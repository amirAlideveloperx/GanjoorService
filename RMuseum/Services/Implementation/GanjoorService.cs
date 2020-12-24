﻿using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RMuseum.DbContext;
using RMuseum.Models.Ganjoor;
using RMuseum.Models.Ganjoor.ViewModels;
using RMuseum.Models.GanjoorAudio;
using RMuseum.Models.GanjoorAudio.ViewModels;
using RMuseum.Models.GanjoorIntegration.ViewModels;
using RSecurityBackend.Models.Generic;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace RMuseum.Services.Implementation
{
    /// <summary>
    /// IGanjoorService implementation
    /// </summary>
    public class GanjoorService : IGanjoorService
    {

        /// <summary>
        /// Get List of poets
        /// </summary>
        /// <param name="websitePoets"></param>
        /// <returns></returns>
        public async Task<RServiceResult<GanjoorPoetViewModel[]>> GetPoets(bool websitePoets)
        {
            try
            {
                
                var res = await _context.GanjoorPoets
                                    .Where(p => !websitePoets || p.Id < 200)
                                    .OrderBy(p => p.Name) //it seems that current version of ef core does not handle this => OrderBy(p => p.Name, StringComparer.Create(new CultureInfo("fa-IR"), true))
                                    .Select
                                    (
                                        p => new GanjoorPoetViewModel()
                                        {
                                            Id = p.Id,
                                            Name = p.Name,
                                            Description = p.Description,
                                            FullUrl = _context.GanjoorCategories.Where(c => c.PoetId == p.Id && c.ParentId == null).Single().FullUrl
                                        }
                                    )
                                    .ToListAsync();
                StringComparer fa = StringComparer.Create(new CultureInfo("fa-IR"), true);
                res.Sort((a, b) => fa.Compare(a.Name, b.Name));

                return new RServiceResult<GanjoorPoetViewModel[]>
                    (
                        res.ToArray()
                    ); ;
            }
            catch (Exception exp)
            {
                return new RServiceResult<GanjoorPoetViewModel[]>(null, exp.ToString());
            }
        }

        /// <summary>
        /// get poet by id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<RServiceResult<GanjoorPoetCompleteViewModel>> GetPoetById(int id)
        {
            try
            {
                var poet = await _context.GanjoorPoets.Where(p => p.Id == id).FirstOrDefaultAsync();
                if (poet == null)
                    return new RServiceResult<GanjoorPoetCompleteViewModel>(null);
                var cat = await _context.GanjoorCategories.Where(c => c.ParentId == null && c.PoetId == id).FirstOrDefaultAsync();
                return await GetCatById(cat.Id);
            }
            catch(Exception exp)
            {
                return new RServiceResult<GanjoorPoetCompleteViewModel>(null, exp.ToString());
            }
        }

        /// <summary>
        /// get cat by id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<RServiceResult<GanjoorPoetCompleteViewModel>> GetCatById(int id)
        {
            try
            {
               
                var cat = await _context.GanjoorCategories.Include(c => c.Poet).Include(c => c.Parent).Where(c => c.Id == id).FirstOrDefaultAsync();
                if (cat == null)
                    return new RServiceResult<GanjoorPoetCompleteViewModel>(null);

                List<GanjoorCatViewModel> ancetors = new List<GanjoorCatViewModel>();

                var parent = cat.Parent;
                while (parent != null)
                {
                    ancetors.Insert(0, new GanjoorCatViewModel()
                    {
                        Id = parent.Id,
                        Title = parent.Title,
                        UrlSlug = parent.UrlSlug
                    });

                    parent = await _context.GanjoorCategories.Where(c => c.Id == parent.ParentId).FirstOrDefaultAsync();
                }

                GanjoorCatViewModel catViewModel = new GanjoorCatViewModel()
                {
                    Id = cat.Id,
                    Title = cat.Title,
                    UrlSlug = cat.UrlSlug,
                    Ancestors = ancetors,
                    Children = await _context.GanjoorCategories.Where(c => c.ParentId == cat.Id).OrderBy(cat => cat.Id).Select
                     (
                     c => new GanjoorCatViewModel()
                     {
                         Id = c.Id,
                         Title = c.Title,
                         UrlSlug = c.UrlSlug
                     }
                     ).ToListAsync(),
                    Poems = await _context.GanjoorPoems.Where(p => p.CatId == cat.Id).OrderBy(p => p.Id).Select
                     (
                         p => new GanjoorPoemSummaryViewModel()
                         {
                             Id = p.Id,
                             Title = p.Title,
                             UrlSlug = p.UrlSlug,
                             Excerpt = _context.GanjoorVerses.Where(v => v.PoemId == p.Id && v.VOrder == 1).FirstOrDefault().Text
                         }
                     ).ToListAsync()
                };

                return new RServiceResult<GanjoorPoetCompleteViewModel>
                   (
                   new GanjoorPoetCompleteViewModel()
                   {
                       Poet = await _context.GanjoorPoets.Where(p => p.Id == cat.PoetId)
                                            .Select(p => new GanjoorPoetViewModel()
                                            {
                                                Id = p.Id,
                                                Name = p.Name,
                                                Description = p.Description,
                                                FullUrl = _context.GanjoorCategories.Where(c => c.PoetId == p.Id && c.ParentId == null).Single().FullUrl
                                            }) .FirstOrDefaultAsync(),
                       Cat = catViewModel
                   }
                   );

            }
            catch (Exception exp)
            {
                return new RServiceResult<GanjoorPoetCompleteViewModel>(null, exp.ToString());
            }
        }


       
        /// <summary>
        /// get poem recitations  (PlainText/HtmlText are intentionally empty)
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<RServiceResult<PublicRecitationViewModel[]>> GetPoemRecitations(int id)
        {
            try
            {
                var source =
                     from audio in _context.Recitations
                     join poem in _context.GanjoorPoems
                     on audio.GanjoorPostId equals poem.Id
                     where
                     audio.ReviewStatus == AudioReviewStatus.Approved
                     &&
                     poem.Id == id
                     orderby audio.AudioOrder
                     select new PublicRecitationViewModel()
                     {
                         Id = audio.Id,
                         PoemId = audio.GanjoorPostId,
                         PoemFullTitle = poem.FullTitle,
                         PoemFullUrl = poem.FullUrl,
                         AudioTitle = audio.AudioTitle,
                         AudioArtist = audio.AudioArtist,
                         AudioArtistUrl = audio.AudioArtistUrl,
                         AudioSrc = audio.AudioSrc,
                         AudioSrcUrl = audio.AudioSrcUrl,
                         LegacyAudioGuid = audio.LegacyAudioGuid,
                         Mp3FileCheckSum = audio.Mp3FileCheckSum,
                         Mp3SizeInBytes = audio.Mp3SizeInBytes,
                         PublishDate = audio.ReviewDate,
                         FileLastUpdated = audio.FileLastUpdated,
                         Mp3Url = $"https://ganjgah.ir/api/audio/file/{audio.Id}.mp3",
                         XmlText = $"https://ganjgah.ir/api/audio/xml/{audio.Id}",
                         PlainText = "", //poem.PlainText 
                         HtmlText = "",//poem.HtmlText
                     };
                return new RServiceResult<PublicRecitationViewModel[]>(await source.ToArrayAsync());
            }
            catch (Exception exp)
            {
                return new RServiceResult<PublicRecitationViewModel[]>(null, exp.ToString());
            }
        }

        /// <summary>
        /// get poem images by id (some fields are intentionally field with blank or null),
        /// EntityImageId : the most important data field, image url is https://ganjgah.ir/api/images/thumb/{EntityImageId}.jpg or https://ganjgah.ir/api/images/norm/{EntityImageId}.jpg
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<RServiceResult<GanjoorLinkViewModel[]>> GetPoemImages(int id)
        {
            try
            {
                var source =
                     from link in _context.GanjoorLinks.Include(l => l.Artifact).Include(l => l.Item).ThenInclude(i => i.Images)
                     join poem in _context.GanjoorPoems
                     on link.GanjoorPostId equals poem.Id
                     where
                     link.ReviewResult == Models.GanjoorIntegration.ReviewResult.Approved
                     &&
                     poem.Id == id
                     orderby link.ReviewDate
                     select new GanjoorLinkViewModel()
                     {
                         Id = link.Id,
                         GanjoorPostId = link.GanjoorPostId,
                         GanjoorUrl = $"https://ganjoor.net{poem.FullUrl}",
                         GanjoorTitle = poem.FullTitle,
                         EntityName = $"{link.Artifact.Name} » {link.Item.Name}", 
                         EntityFriendlyUrl = $"https://museum.ganjoor.net/items/{link.Artifact.FriendlyUrl}/{link.Item.FriendlyUrl}",
                         EntityImageId = link.Item.Images.First().Id,//the most important data field, image url is https://ganjgah.ir/api/images/thumb/{EntityImageId}.jpg
                         ReviewResult = link.ReviewResult,
                         Synchronized = link.Synchronized,
                         SuggestedBy = null //intentional (this is going to be used in an anonymous method)
                     };
                return new RServiceResult<GanjoorLinkViewModel[]>(await source.ToArrayAsync());
            }
            catch (Exception exp)
            {
                return new RServiceResult<GanjoorLinkViewModel[]>(null, exp.ToString());
            }
        }

        private int _GetRandomPoemId()
        {
            //this is magic number based method!
            int startPoemId = 2130;
            int endPoemId = 2624 + 1; //one is added for مژده ای دل که مسیحا نفسی می‌آید
            Random r = new Random(DateTime.Now.Millisecond);
            int poemId = r.Next(startPoemId, endPoemId);
            if (poemId == endPoemId)
            {
                poemId = 33179;//مژده ای دل که مسیحا نفسی می‌آید
            }
            return poemId;
        }

        /// <summary>
        /// Get Poem By Id
        /// </summary>
        /// <param name="id"></param>
        /// <param name="catInfo"></param>
        /// <param name="rhymes"></param>
        /// <param name="recitations"></param>
        /// <param name="images"></param>
        /// <param name="songs"></param>
        /// <param name="comments"></param>
        /// <returns></returns>
        public async Task<RServiceResult<GanjoorPoemCompleteViewModel>> GetPoemById(int id, bool catInfo = true, bool rhymes = true, bool recitations = true, bool images = true, bool songs = true, bool comments = true)
        {
            try
            {
                var poem = await _context.GanjoorPoems.Where(p => p.Id == id).SingleOrDefaultAsync();
                if(poem == null)
                {
                    return new RServiceResult<GanjoorPoemCompleteViewModel>(null); //not found
                }
                GanjoorPoetCompleteViewModel cat = null;
                if(catInfo)
                {
                    var catRes = await GetCatById(poem.CatId);
                    if(!string.IsNullOrEmpty(catRes.ExceptionString))
                    {
                        return new RServiceResult<GanjoorPoemCompleteViewModel>(null, catRes.ExceptionString);
                    }
                }
                PublicRecitationViewModel[] rc = null;
                if(recitations)
                {
                    var rcRes = await GetPoemRecitations(id);
                    if (!string.IsNullOrEmpty(rcRes.ExceptionString))
                        return new RServiceResult<GanjoorPoemCompleteViewModel>(null, rcRes.ExceptionString);
                    rc = rcRes.Result;
                }

                GanjoorLinkViewModel[] imgs = null;
                if(images)
                {
                    var imgsRes = await GetPoemImages(id);
                    if (!string.IsNullOrEmpty(imgsRes.ExceptionString))
                        return new RServiceResult<GanjoorPoemCompleteViewModel>(null, imgsRes.ExceptionString);
                    imgs = imgsRes.Result;
                }



                return new RServiceResult<GanjoorPoemCompleteViewModel>
                    (
                    new GanjoorPoemCompleteViewModel()
                    {
                        Id = poem.Id,
                        Title = poem.Title,
                        FullTitle = poem.FullTitle,
                        FullUrl = poem.FullUrl,
                        UrlSlug = poem.UrlSlug,
                        HtmlText = poem.HtmlText,
                        PlainText = poem.PlainText,
                        Category = cat,
                        Recitations = rc,
                        Images = imgs
                    }
                    );
            }
            catch (Exception exp)
            {
                return new RServiceResult<GanjoorPoemCompleteViewModel>(null, exp.ToString());
            }
        }

        /// <summary>
        /// get a random poem from hafez
        /// </summary>
        /// <returns></returns>
        public async Task<RServiceResult<GanjoorPoemCompleteViewModel>> Faal()
        {
            try
            {
                int poemId = _GetRandomPoemId();
                var poem = await _context.GanjoorPoems.Where(p => p.Id == poemId).SingleOrDefaultAsync();
                PublicRecitationViewModel[] recitations = poem == null ? new PublicRecitationViewModel[] { } : (await GetPoemRecitations(poemId)).Result;
                int loopPreventer = 0;
                while (poem == null || recitations.Length == 0)
                {
                    poem = await _context.GanjoorPoems.Where(p => p.Id == poemId).SingleOrDefaultAsync();
                    recitations = poem == null ? new PublicRecitationViewModel[] { } : (await GetPoemRecitations(poemId)).Result;
                    loopPreventer++;
                    if (loopPreventer > 5)
                        break;
                }

                return await GetPoemById(poemId, false, false, true, false, false, false);
            }
            catch (Exception exp)
            {
                return new RServiceResult<GanjoorPoemCompleteViewModel>(null, exp.ToString());
            }
        }

        #region Date import / modifications
        /// <summary>
        /// imports unimported poem data from a locally accessible ganjoor SqlLite database
        /// </summary>
        /// <returns></returns>
        public async Task<RServiceResult<bool>> ImportLocalSQLiteDb()
        {
            try
            {
                SqliteConnectionStringBuilder connectionStringBuilder = new SqliteConnectionStringBuilder();
                connectionStringBuilder.DataSource = Configuration.GetSection("LocalSqliteImport")["FilePath"];
                using (SqliteConnection sqliteConnection = new SqliteConnection(connectionStringBuilder.ToString()))
                {
                    await sqliteConnection.OpenAsync();
                    IDbConnection dapper = sqliteConnection;
                    using (var sqlConnection = _context.Database.GetDbConnection())
                    {
                        await sqlConnection.OpenAsync();
                        
                            foreach (var poet in await dapper.QueryAsync("SELECT * FROM poet ORDER BY id"))
                            {
                                int poetId = (int)poet.id;
                                if ((await _context.GanjoorPoets.Where(p => p.Id == poetId).FirstOrDefaultAsync()) != null)
                                {
                                    continue;
                                }

                                DbTransaction transaction = await sqlConnection.BeginTransactionAsync();
                                try
                                {
                                    using (var command = sqlConnection.CreateCommand())
                                    {
                                        command.Transaction = transaction;
                                        command.CommandText =
                                            $"INSERT INTO GanjoorPoets (Id, Name, Description) VALUES (${poet.id}, N'{poet.name}', N'{poet.description}')";
                                        await command.ExecuteNonQueryAsync();
                                        await _ImportSQLiteCatChildren(command, dapper, poetId, 0, "", "");
                                        await transaction.CommitAsync();
                                    }
                                }
                                catch (Exception exp2)
                                {
                                    await transaction.RollbackAsync();
                                    return new RServiceResult<bool>(false, exp2.ToString());
                                }
                            
                               
                            }
                    }
                        
                }
            }
            catch(Exception exp)
            {
                return new RServiceResult<bool>(false, exp.ToString());
            }
            return new RServiceResult<bool>(true);
        }
        private async Task _ImportSQLiteCatChildren(DbCommand command, IDbConnection dapper, int poetId, int catId, string itemFullTitle, string itemFullSlug)
        {
           
            foreach(var cat in await dapper.QueryAsync($"SELECT * FROM cat WHERE poet_id = {poetId} AND parent_id = {catId} ORDER BY id"))
            {
               
                if (catId == 0)
                {
                    command.CommandText =
                                  $"INSERT INTO GanjoorCategories (Id, PoetId, Title, UrlSlug, FullUrl) VALUES (${cat.id}, {poetId}, N'{cat.text}', '{ _ExtractUrlSlug(cat.url)}', '{itemFullSlug}/{ _ExtractUrlSlug(cat.url)}')";
                }
                else
                {
                    command.CommandText =
                                  $"INSERT INTO GanjoorCategories (Id, PoetId, Title, ParentId, UrlSlug, FullUrl) VALUES (${cat.id}, {poetId}, N'{cat.text}', {catId}, '{ _ExtractUrlSlug(cat.url)}', '{itemFullSlug}/{ _ExtractUrlSlug(cat.url)}')";
                }
               
                await command.ExecuteNonQueryAsync();


                foreach (var poem in await dapper.QueryAsync($"SELECT * FROM poem WHERE cat_id = {cat.id} ORDER BY id"))
                {
                    command.CommandText =
                               $"INSERT INTO GanjoorPoems (Id, CatId, Title, UrlSlug, FullTitle, FullUrl) VALUES (${poem.id}, {cat.id}, N'{poem.title}', '{ _ExtractUrlSlug(poem.url)}', N'{$"{itemFullTitle}{cat.text} » {poem.title}"}', '{itemFullSlug}/{ _ExtractUrlSlug(cat.url)}/{_ExtractUrlSlug(poem.url)}')";
                    await command.ExecuteNonQueryAsync();

                    foreach (var verse in await dapper.QueryAsync($"SELECT * FROM verse WHERE poem_id = {poem.id} ORDER BY vorder"))
                    {
                        command.CommandText =
                              $"INSERT INTO GanjoorVerses (PoemId, VOrder, VersePosition, Text) VALUES (${poem.id}, {verse.vorder}, {verse.position}, N'{verse.text}')";
                        await command.ExecuteNonQueryAsync();
                    }

                }


                await _ImportSQLiteCatChildren(command, dapper, poetId, (int)cat.id, $"{itemFullTitle}{cat.text} » ", $"{itemFullSlug}/{ _ExtractUrlSlug(cat.url)}");
                

            }
        }
        private static string _ExtractUrlSlug(string slug)
        {
            if (!string.IsNullOrEmpty(slug))
            {
                //sample: https://ganjoor.net/hafez
                if (slug.LastIndexOf('/') != -1)
                {
                    // => result = hafez
                    slug = slug.Substring(slug.LastIndexOf('/') + 1);
                }
            }
            return slug;
        }

        /// <summary>
        /// updates poems text
        /// </summary>
        /// <returns></returns>
        public RServiceResult<bool> UpdatePoemsText()
        {
            try
            {
                _backgroundTaskQueue.QueueBackgroundWorkItem
                (
                async token =>
                {
                    using (RMuseumDbContext context = new RMuseumDbContext(Configuration)) //this is long running job, so _context might be already been freed/collected by GC
                    {
                        var poems = await context.GanjoorPoems.Where(p => p.PlainText == null).ToListAsync();
                        int n = 0;
                        foreach (GanjoorPoem poem in poems)
                        {
                            var verses = await context.GanjoorVerses.Where(v => v.PoemId == poem.Id).OrderBy(v => v.VOrder).ToListAsync();
                            string plainText = "";
                            string htmlText = "";
                            for (int vIndex = 0; vIndex < verses.Count; vIndex++)
                            {
                                GanjoorVerse v = verses[vIndex];

                                if (string.IsNullOrEmpty(plainText))
                                    plainText = v.Text;
                                else
                                {
                                    plainText += $" {v.Text}";
                                }

                                switch (v.VersePosition)
                                {
                                    case VersePosition.CenteredVerse1:
                                        if (((vIndex + 1) < verses.Count) && (verses[vIndex + 1].VersePosition == VersePosition.CenteredVerse2))
                                        {
                                            htmlText += $"<div class=\"b2\"><p>{v.Text.Replace("ـ", "").Trim()}</p>{Environment.NewLine}"; //div is not closed
                                        }
                                        else
                                        {
                                            htmlText += $"<div class=\"b2\"><p>{v.Text.Replace("ـ", "").Trim()}</p></div>{Environment.NewLine}";
                                        }
                                        break;
                                    case VersePosition.CenteredVerse2:
                                        htmlText += $"<p>{v.Text.Replace("ـ", "").Trim()}</p></div>{Environment.NewLine}";
                                        break;
                                    case VersePosition.Right:
                                        htmlText += $"<div class=\"b\"><div class=\"m1\"><p>{v.Text.Replace("ـ", "").Trim()}</p></div>{Environment.NewLine}";
                                        break;
                                    case VersePosition.Left:
                                        htmlText += $"<div class=\"m2\"><p>{v.Text.Replace("ـ", "").Trim()}</p></div></div>{Environment.NewLine}";
                                        break;
                                    case VersePosition.Paragraph:
                                    case VersePosition.Single:
                                        {
                                            string[] lines = v.Text.Replace("ـ", "").Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                                            if (lines.Length != 0)
                                            {
                                                if (v.Text.Replace("ـ", "").Length / lines.Length < 150)
                                                {
                                                    htmlText += $"<div class=\"n\"><p>{v.Text.Replace("ـ", "").Replace("\r\n", " ")}</p></div>{Environment.NewLine}";
                                                }
                                                else
                                                {
                                                    foreach (string line in lines)
                                                        htmlText += $"<div class=\"n\"><p>{line}</p></div>{Environment.NewLine}";
                                                }
                                            }
                                        }
                                        break;
                                }
                            }

                            poem.PlainText = plainText;
                            poem.HtmlText = htmlText;

                            context.Update(poem);

                            n++;

                            if (n >= 1000)
                            {
                                await context.SaveChangesAsync();
                                n = 0;
                            }
                        }

                        if(n > 0)
                            await context.SaveChangesAsync();


                    }


                });


                return new RServiceResult<bool>(true);
            }
            catch(Exception exp)
            {
                return new RServiceResult<bool>(false, exp.ToString());
            }

        }
        #endregion

        /// <summary>
        /// Database Contetxt
        /// </summary>
        protected readonly RMuseumDbContext _context;

        /// <summary>
        /// Configuration
        /// </summary>
        protected IConfiguration Configuration { get; }

        /// <summary>
        /// Background Task Queue Instance
        /// </summary>
        protected readonly IBackgroundTaskQueue _backgroundTaskQueue;

        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="context"></param>
        /// <param name="configuration"></param>
        /// <param name="backgroundTaskQueue"></param>
        public GanjoorService(RMuseumDbContext context, IConfiguration configuration, IBackgroundTaskQueue backgroundTaskQueue)
        {
            _context = context;
            _backgroundTaskQueue = backgroundTaskQueue;
            Configuration = configuration;
        }
    }
}
