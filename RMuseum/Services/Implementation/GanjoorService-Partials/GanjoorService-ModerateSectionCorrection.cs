﻿using Microsoft.EntityFrameworkCore;
using RMuseum.DbContext;
using RMuseum.Models.Ganjoor;
using RMuseum.Models.Ganjoor.ViewModels;
using RSecurityBackend.Models.Generic;
using RSecurityBackend.Services.Implementation;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace RMuseum.Services.Implementation
{
    /// <summary>
    /// IGanjoorService implementation
    /// </summary>
    public partial class GanjoorService : IGanjoorService
    {
        /// <summary>
        /// moderate poem section correction
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="moderation"></param>
        /// <returns></returns>
        public async Task<RServiceResult<GanjoorPoemSectionCorrectionViewModel>> ModeratePoemSectionCorrection(Guid userId,
            GanjoorPoemSectionCorrectionViewModel moderation)
        {

            try
            {
                var dbCorrection = await _context.GanjoorPoemSectionCorrections.Include(c => c.User)
                .Where(c => c.Id == moderation.Id)
                .FirstOrDefaultAsync();

                if (dbCorrection == null)
                    return new RServiceResult<GanjoorPoemSectionCorrectionViewModel>(null);

                dbCorrection.ReviewerUserId = userId;
                dbCorrection.ReviewDate = DateTime.Now;
                dbCorrection.ApplicationOrder = await _context.GanjoorPoemSectionCorrections.Where(c => c.Reviewed).AnyAsync() ? 1 + await _context.GanjoorPoemSectionCorrections.Where(c => c.Reviewed).MaxAsync(c => c.ApplicationOrder) : 1;
                dbCorrection.Reviewed = true;
                dbCorrection.AffectedThePoem = false;
                dbCorrection.ReviewNote = moderation.ReviewNote;

                var editingSectionNotTracked = await _context.GanjoorPoemSections.AsNoTracking().Include(p => p.GanjoorMetre).Where(p => p.Id == moderation.SectionId).SingleOrDefaultAsync();

                editingSectionNotTracked.OldGanjoorMetreId = editingSectionNotTracked.GanjoorMetreId;
                editingSectionNotTracked.OldRhymeLetters = editingSectionNotTracked.RhymeLetters;

                var sections = await _context.GanjoorPoemSections.Where(p => p.PoemId == editingSectionNotTracked.PoemId).ToListAsync();


                if (dbCorrection.Rhythm != null)
                {
                    if (moderation.RhythmResult == CorrectionReviewResult.NotReviewed)
                        return new RServiceResult<GanjoorPoemSectionCorrectionViewModel>(null, "تغییرات وزن بررسی نشده است.");
                    dbCorrection.RhythmResult = moderation.RhythmResult;
                    if (dbCorrection.RhythmResult == CorrectionReviewResult.Approved)
                    {
                        dbCorrection.AffectedThePoem = true;
                        if (moderation.Rhythm == "")
                        {
                            editingSectionNotTracked.GanjoorMetreId = null;
                        }
                        else
                        {
                            var metre = await _context.GanjoorMetres.AsNoTracking().Where(m => m.Rhythm == moderation.Rhythm).SingleOrDefaultAsync();
                            if (metre == null)
                            {
                                metre = new GanjoorMetre()
                                {
                                    Rhythm = moderation.Rhythm,
                                    VerseCount = 0
                                };
                                _context.GanjoorMetres.Add(metre);
                                await _context.SaveChangesAsync();
                            }
                            editingSectionNotTracked.GanjoorMetreId = metre.Id;
                        }

                        editingSectionNotTracked.Modified = editingSectionNotTracked.OldGanjoorMetreId != editingSectionNotTracked.GanjoorMetreId;

                        foreach (var section in sections.Where(s => s.GanjoorMetreRefSectionIndex == editingSectionNotTracked.Index || s.Id == editingSectionNotTracked.Id))
                        {
                            section.GanjoorMetreId = editingSectionNotTracked.GanjoorMetreId;
                            section.Modified = editingSectionNotTracked.Modified;
                            _context.Update(section);
                        }
                    }
                }

                if (dbCorrection.BreakFromVerse4VOrder != null)
                {
                    if (moderation.BreakFromVerse4VOrder != dbCorrection.BreakFromVerse4VOrder)
                    {
                        dbCorrection.BreakFromVerse4VOrderResult = CorrectionReviewResult.RejectedBecauseWrong;
                        dbCorrection.BreakFromVerse4VOrder = null;
                    }
                    else
                    {
                        await _BreakSection(sections, editingSectionNotTracked, (int)dbCorrection.BreakFromVerse4VOrder);
                        dbCorrection.BreakFromVerse4VOrderResult = CorrectionReviewResult.Approved;
                        _context.GanjoorPoemSectionCorrections.Update(dbCorrection);
                        await _context.SaveChangesAsync();
                    }
                }

                if (dbCorrection.BreakFromVerse3VOrder != null)
                {
                    if (moderation.BreakFromVerse3VOrder != dbCorrection.BreakFromVerse3VOrder)
                    {
                        dbCorrection.BreakFromVerse3VOrderResult = CorrectionReviewResult.RejectedBecauseWrong;
                        dbCorrection.BreakFromVerse3VOrder = null;
                    }
                    else
                    {
                        await _BreakSection(sections, editingSectionNotTracked, (int)dbCorrection.BreakFromVerse3VOrder);
                        dbCorrection.BreakFromVerse3VOrderResult = CorrectionReviewResult.Approved;
                        _context.GanjoorPoemSectionCorrections.Update(dbCorrection);
                        await _context.SaveChangesAsync();
                    }
                }

                if (dbCorrection.BreakFromVerse2VOrder != null)
                {
                    if (moderation.BreakFromVerse2VOrder != dbCorrection.BreakFromVerse2VOrder)
                    {
                        dbCorrection.BreakFromVerse2VOrderResult = CorrectionReviewResult.RejectedBecauseWrong;
                        dbCorrection.BreakFromVerse2VOrder = null;
                    }
                    else
                    {
                        await _BreakSection(sections, editingSectionNotTracked, (int)dbCorrection.BreakFromVerse2VOrder);
                        dbCorrection.BreakFromVerse2VOrderResult = CorrectionReviewResult.Approved;
                        _context.GanjoorPoemSectionCorrections.Update(dbCorrection);
                        await _context.SaveChangesAsync();
                    }
                }

                if (dbCorrection.BreakFromVerse1VOrder != null)
                {
                    if (moderation.BreakFromVerse1VOrder != dbCorrection.BreakFromVerse1VOrder)
                    {
                        dbCorrection.BreakFromVerse1VOrderResult = CorrectionReviewResult.RejectedBecauseWrong;
                        dbCorrection.BreakFromVerse1VOrder = null;
                    }
                    else
                    {
                        await _BreakSection(sections, editingSectionNotTracked, (int)dbCorrection.BreakFromVerse1VOrder);
                        dbCorrection.BreakFromVerse1VOrderResult = CorrectionReviewResult.Approved;
                        _context.GanjoorPoemSectionCorrections.Update(dbCorrection);
                        await _context.SaveChangesAsync();
                    }
                }
                _context.GanjoorPoemSectionCorrections.Update(dbCorrection);
                await _context.SaveChangesAsync();

                var dbPoem = await _context.GanjoorPoems.AsNoTracking().Where(p => p.Id == editingSectionNotTracked.PoemId).SingleAsync();

                await _notificationService.PushNotification(dbCorrection.UserId,
                                   "بررسی ویرایش پیشنهادی شما",
                                   $"با سپاس از زحمت و همت شما ویرایش پیشنهادیتان برای <a href=\"{dbPoem.FullUrl}\" target=\"_blank\">{dbPoem.FullTitle}</a> بررسی شد.{Environment.NewLine}" +
                                   $"جهت مشاهدهٔ نتیجهٔ بررسی در میز کاربری خود بخش «ویرایش‌های من» را مشاهده بفرمایید.{Environment.NewLine}"
                                   );

                foreach (var section in sections)
                {
                    if (section.Modified)
                    {
                        if (section.OldGanjoorMetreId != section.GanjoorMetreId || section.OldRhymeLetters != section.RhymeLetters)
                        {
                            _backgroundTaskQueue.QueueBackgroundWorkItem
                                    (
                                    async token =>
                                    {
                                        using (RMuseumDbContext inlineContext = new RMuseumDbContext(new DbContextOptions<RMuseumDbContext>())) //this is long running job, so context might be already been freed/collected by GC
                                        {
                                            if (section.OldGanjoorMetreId != null && !string.IsNullOrEmpty(section.OldRhymeLetters))
                                            {
                                                LongRunningJobProgressServiceEF jobProgressServiceEF = new LongRunningJobProgressServiceEF(inlineContext);
                                                var job = (await jobProgressServiceEF.NewJob($"بازسازی فهرست بخش‌های مرتبط", $"M: {section.OldGanjoorMetreId}, G: {section.OldRhymeLetters}")).Result;

                                                try
                                                {
                                                    await _UpdateRelatedSections(inlineContext, (int)section.OldGanjoorMetreId, section.OldRhymeLetters);
                                                    await inlineContext.SaveChangesAsync();

                                                    await jobProgressServiceEF.UpdateJob(job.Id, 100, "", true);
                                                }
                                                catch (Exception exp)
                                                {
                                                    await jobProgressServiceEF.UpdateJob(job.Id, 100, "", false, exp.ToString());
                                                }
                                            }

                                            if (section.GanjoorMetreId != null && !string.IsNullOrEmpty(section.RhymeLetters))
                                            {
                                                LongRunningJobProgressServiceEF jobProgressServiceEF = new LongRunningJobProgressServiceEF(inlineContext);
                                                var job = (await jobProgressServiceEF.NewJob($"بازسازی فهرست بخش‌های مرتبط", $"M: {section.GanjoorMetreId}, G: {section.RhymeLetters}")).Result;

                                                try
                                                {
                                                    await _UpdateRelatedSections(inlineContext, (int)section.GanjoorMetreId, section.RhymeLetters);
                                                    await inlineContext.SaveChangesAsync();
                                                    await jobProgressServiceEF.UpdateJob(job.Id, 100, "", true);
                                                }
                                                catch (Exception exp)
                                                {
                                                    await jobProgressServiceEF.UpdateJob(job.Id, 100, "", false, exp.ToString());
                                                }
                                            }
                                        }
                                    });
                        }
                    }
                }

                return new RServiceResult<GanjoorPoemSectionCorrectionViewModel>(moderation);
            }
            catch (Exception exp)
            {
                return new RServiceResult<GanjoorPoemSectionCorrectionViewModel>(null, exp.ToString());
            }
        }


        private async Task _BreakSection(List<GanjoorPoemSection> sections, GanjoorPoemSection editingSectionNotTracked, int vOrder)
        {
            
            var newSectionIndex = sections.Max(s => s.Index) + 1;
            var newSectionNumber = sections.Max(s => s.Number) + 1;
            int breakingMainSectionIndex = newSectionIndex;
            var sectionCopy = new GanjoorPoemSection()
            {
                PoemId = editingSectionNotTracked.PoemId,
                PoetId = editingSectionNotTracked.PoetId,
                SectionType = editingSectionNotTracked.SectionType,
                VerseType = editingSectionNotTracked.VerseType,
                Index = newSectionIndex,
                Number = newSectionNumber,
                PoemFormat = editingSectionNotTracked.PoemFormat,
            };
            var verses = await _context.GanjoorVerses.Where(v => v.PoemId == editingSectionNotTracked.PoemId).OrderBy(v => v.VOrder).ToListAsync();
            var editingSectionVerses = FilterSectionVerses(editingSectionNotTracked, verses);

            foreach (var verse in editingSectionVerses)
            {
                if (verse.VOrder >= vOrder)
                {
                    verse.SectionIndex1 = newSectionIndex;
                    _context.Update(verse);
                }
            }

            bool sectionCopyBecameMasnavi = false;

            var sectionCopyVerses = FilterSectionVerses(sectionCopy, verses);
            sectionCopy.HtmlText = PrepareHtmlText(sectionCopyVerses);
            sectionCopy.PlainText = PreparePlainText(sectionCopyVerses);
            sectionCopy.RhymeLetters = LanguageUtils.FindRhyme(sectionCopyVerses).Rhyme;
            sectionCopy.CachedFirstCoupletIndex = (int)sectionCopyVerses.Min(v => v.CoupletIndex);
            _context.Add(sectionCopy);
            sectionCopy.Modified = true;

            if(sectionCopy.PoemFormat != GanjoorPoemFormat.Masnavi)
            {
                if(_IsMasnavi(sectionCopyVerses))
                {
                    sectionCopy.PoemFormat = GanjoorPoemFormat.Masnavi;
                    sectionCopyBecameMasnavi = true;
                }
            }
            sections.Add(sectionCopy);

            bool updatingBecameMasnavi = false;

            var updatingSection = sections.Where(s => s.Id == editingSectionNotTracked.Id).Single();
            var updatingSectionVerses = FilterSectionVerses(updatingSection, verses);
            updatingSection.HtmlText = PrepareHtmlText(updatingSectionVerses);
            updatingSection.PlainText = PreparePlainText(updatingSectionVerses);
            updatingSection.RhymeLetters = LanguageUtils.FindRhyme(updatingSectionVerses).Rhyme;
            updatingSection.Modified = true;
            if (updatingSection.PoemFormat != GanjoorPoemFormat.Masnavi)
            {
                if (_IsMasnavi(updatingSectionVerses))
                {
                    updatingSection.PoemFormat = GanjoorPoemFormat.Masnavi;
                    updatingBecameMasnavi = true;
                }
            }
            _context.Update(updatingSection);

            List<GanjoorPoemSection> addedSections = new List<GanjoorPoemSection>();
            foreach (var relatedSection in sections.Where(s => s.GanjoorMetreRefSectionIndex == editingSectionNotTracked.Index))
            {
                var relatedSectionVerses = FilterSectionVerses(relatedSection, verses);
                if (relatedSectionVerses.Any(v => v.VOrder >= vOrder))
                {
                    if(relatedSectionVerses.Any(v => v.VOrder < vOrder))
                    {
                        newSectionIndex++;
                        newSectionNumber++;
                        var relatedSectionCopy = new GanjoorPoemSection()
                        {
                            PoemId = relatedSection.PoemId,
                            PoetId = relatedSection.PoetId,
                            SectionType = relatedSection.SectionType,
                            VerseType = relatedSection.VerseType,
                            Index = newSectionIndex,
                            Number = newSectionNumber,
                            PoemFormat = relatedSection.PoemFormat,
                            GanjoorMetreRefSectionIndex = breakingMainSectionIndex,
                        };

                        foreach (var verse in relatedSectionVerses)
                        {
                            if (verse.VOrder >= vOrder)
                            {
                                switch (relatedSection.VerseType)
                                {
                                    case VersePoemSectionType.Third:
                                        verse.SectionIndex3 = newSectionIndex;
                                        break;
                                    case VersePoemSectionType.Forth:
                                        verse.SectionIndex4 = newSectionIndex;
                                        break;
                                    default:
                                        verse.SectionIndex2 = newSectionIndex;
                                        break;
                                }
                                _context.Update(verse);
                            }
                        }

                        var relatedSectionCopyVerses = FilterSectionVerses(relatedSectionCopy, verses);
                        relatedSectionCopy.HtmlText = PrepareHtmlText(relatedSectionCopyVerses);
                        relatedSectionCopy.PlainText = PreparePlainText(relatedSectionCopyVerses);
                        relatedSectionCopy.RhymeLetters = LanguageUtils.FindRhyme(relatedSectionCopyVerses).Rhyme;
                        relatedSectionCopy.Modified = true;
                        relatedSectionCopy.CachedFirstCoupletIndex = (int)relatedSectionCopyVerses.Min(v => v.CoupletIndex);
                        _context.Add(relatedSectionCopy);
                        addedSections.Add(relatedSectionCopy);

                        relatedSectionVerses = FilterSectionVerses(relatedSection, verses);
                        relatedSection.HtmlText = PrepareHtmlText(relatedSectionVerses);
                        relatedSection.PlainText = PreparePlainText(relatedSectionVerses);
                        relatedSection.RhymeLetters = LanguageUtils.FindRhyme(relatedSectionVerses).Rhyme;
                        relatedSection.Modified = true;
                        _context.Update(relatedSection);
                    }
                    else
                    {
                        relatedSection.GanjoorMetreRefSectionIndex = breakingMainSectionIndex;
                    }
                }
            }

            if(sectionCopyBecameMasnavi)
            {
                for (int v = 0; v < sectionCopyVerses.Count; v += 2)
                {
                    newSectionIndex++;
                    newSectionNumber++;
                    var rightVerse = sectionCopyVerses[v];
                    var leftVerse = sectionCopyVerses[v + 1];
                    List<GanjoorVerse> coupletVerses = new List<GanjoorVerse>();
                    coupletVerses.Add(rightVerse);
                    coupletVerses.Add(leftVerse);
                    var res = LanguageUtils.FindRhyme(coupletVerses);

                    GanjoorPoemSection verseSection = new GanjoorPoemSection()
                    {
                        PoemId = sectionCopy.PoemId,
                        PoetId = sectionCopy.PoetId,
                        SectionType = PoemSectionType.Couplet,
                        VerseType = VersePoemSectionType.Second,
                        Index = newSectionIndex,
                        Number = newSectionNumber,//couplet number
                        GanjoorMetreId = sectionCopy.GanjoorMetreId,
                        RhymeLetters = res.Rhyme,
                        GanjoorMetreRefSectionIndex = sectionCopy.Index,
                    };

                    rightVerse.SectionIndex2 = verseSection.Index;
                    leftVerse.SectionIndex2 = verseSection.Index;

                    var rl = new List<GanjoorVerse>(); rl.Add(rightVerse); rl.Add(leftVerse);
                    verseSection.HtmlText = PrepareHtmlText(rl);
                    verseSection.PlainText = PreparePlainText(rl);
                    verseSection.Modified = true;
                    addedSections.Add(verseSection);
                    verseSection.CachedFirstCoupletIndex = (int)rl.Min(v => v.CoupletIndex);
                    _context.GanjoorPoemSections.Add(verseSection);
                }
            }
            if(updatingBecameMasnavi)
            {
                for (int v = 0; v < updatingSectionVerses.Count; v += 2)
                {
                    newSectionIndex++;
                    newSectionNumber++;
                    var rightVerse = updatingSectionVerses[v];
                    var leftVerse = updatingSectionVerses[v + 1];
                    List<GanjoorVerse> coupletVerses = new List<GanjoorVerse>();
                    coupletVerses.Add(rightVerse);
                    coupletVerses.Add(leftVerse);
                    var res = LanguageUtils.FindRhyme(coupletVerses);

                    GanjoorPoemSection verseSection = new GanjoorPoemSection()
                    {
                        PoemId = updatingSection.PoemId,
                        PoetId = updatingSection.PoetId,
                        SectionType = PoemSectionType.Couplet,
                        VerseType = VersePoemSectionType.Second,
                        Index = newSectionIndex,
                        Number = newSectionNumber,//couplet number
                        GanjoorMetreId = updatingSection.GanjoorMetreId,
                        RhymeLetters = res.Rhyme,
                        GanjoorMetreRefSectionIndex = updatingSection.Index,
                    };

                    rightVerse.SectionIndex2 = verseSection.Index;
                    leftVerse.SectionIndex2 = verseSection.Index;

                    var rl = new List<GanjoorVerse>(); rl.Add(rightVerse); rl.Add(leftVerse);
                    verseSection.HtmlText = PrepareHtmlText(rl);
                    verseSection.PlainText = PreparePlainText(rl);
                    verseSection.Modified = true;
                    verseSection.CachedFirstCoupletIndex = (int)rl.Min(v => v.CoupletIndex);
                    addedSections.Add(verseSection);
                    _context.GanjoorPoemSections.Add(verseSection);
                }
            }
            if (addedSections.Count > 0)
                sections.AddRange(addedSections);
        }

        /// <summary>
        /// get user section corrections
        /// </summary>
        /// <param name="userId">if sent empty returns all corrections</param>
        /// <param name="paging"></param>
        /// <returns></returns>
        public async Task<RServiceResult<(PaginationMetadata PagingMeta, GanjoorPoemSectionCorrectionViewModel[] Items)>> GetUserSectionCorrections(Guid userId, PagingParameterModel paging)
        {
            var source = from dbCorrection in
                             _context.GanjoorPoemSectionCorrections.AsNoTracking().Include(c => c.User)
                         where userId == Guid.Empty || dbCorrection.UserId == userId
                         orderby dbCorrection.Id descending
                         select
                          dbCorrection;

            (PaginationMetadata PagingMeta, GanjoorPoemSectionCorrection[] Items) dbPaginatedResult =
                await QueryablePaginator<GanjoorPoemSectionCorrection>.Paginate(source, paging);

            List<GanjoorPoemSectionCorrectionViewModel> list = new List<GanjoorPoemSectionCorrectionViewModel>();
            foreach (var dbCorrection in dbPaginatedResult.Items)
            {
                list.Add
                    (
                new GanjoorPoemSectionCorrectionViewModel()
                {
                    Id = dbCorrection.Id,
                    SectionId = dbCorrection.SectionId,
                    UserId = dbCorrection.UserId,
                    Rhythm = dbCorrection.Rhythm,
                    OriginalRhythm = dbCorrection.OriginalRhythm,
                    RhythmResult = dbCorrection.RhythmResult,
                    Rhythm2 = dbCorrection.Rhythm2,
                    OriginalRhythm2 = dbCorrection.OriginalRhythm2,
                    RhythmResult2 = dbCorrection.RhythmResult2,
                    BreakFromVerse1VOrder = dbCorrection.BreakFromVerse1VOrder,
                    BreakFromVerse1VOrderResult = dbCorrection.BreakFromVerse1VOrderResult,
                    BreakFromVerse2VOrder = dbCorrection.BreakFromVerse2VOrder,
                    BreakFromVerse2VOrderResult = dbCorrection.BreakFromVerse2VOrderResult,
                    BreakFromVerse3VOrder = dbCorrection.BreakFromVerse3VOrder,
                    BreakFromVerse3VOrderResult = dbCorrection.BreakFromVerse3VOrderResult,
                    BreakFromVerse4VOrder = dbCorrection.BreakFromVerse4VOrder,
                    BreakFromVerse4VOrderResult = dbCorrection.BreakFromVerse4VOrderResult,
                    Note = dbCorrection.Note,
                    Date = dbCorrection.Date,
                    Reviewed = dbCorrection.Reviewed,
                    ReviewNote = dbCorrection.ReviewNote,
                    ReviewDate = dbCorrection.ReviewDate,
                    UserNickname = string.IsNullOrEmpty(dbCorrection.User.NickName) ? dbCorrection.User.Id.ToString() : dbCorrection.User.NickName
                }
                );
            }

            return new RServiceResult<(PaginationMetadata, GanjoorPoemSectionCorrectionViewModel[])>
                ((dbPaginatedResult.PagingMeta, list.ToArray()));
        }


        /// <summary>
        /// last unreviewed user correction for a section
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="sectionId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<GanjoorPoemSectionCorrectionViewModel>> GetLastUnreviewedUserCorrectionForSection(Guid userId, int sectionId)
        {
            var dbCorrection = await _context.GanjoorPoemSectionCorrections.AsNoTracking().Include(c => c.User)
                .Where(c => c.UserId == userId && c.SectionId == sectionId && c.Reviewed == false)
                .OrderByDescending(c => c.Id)
                .FirstOrDefaultAsync();

            if (dbCorrection == null)
                return new RServiceResult<GanjoorPoemSectionCorrectionViewModel>(null);

            return new RServiceResult<GanjoorPoemSectionCorrectionViewModel>
                (
                new GanjoorPoemSectionCorrectionViewModel()
                {
                    Id = dbCorrection.Id,
                    SectionId = dbCorrection.SectionId,
                    UserId = dbCorrection.UserId,
                    Rhythm = dbCorrection.Rhythm,
                    OriginalRhythm = dbCorrection.OriginalRhythm,
                    RhythmResult = dbCorrection.RhythmResult,
                    Rhythm2 = dbCorrection.Rhythm2,
                    OriginalRhythm2 = dbCorrection.OriginalRhythm2,
                    RhythmResult2 = dbCorrection.RhythmResult2,
                    BreakFromVerse1VOrder = dbCorrection.BreakFromVerse1VOrder,
                    BreakFromVerse1VOrderResult = dbCorrection.BreakFromVerse1VOrderResult,
                    BreakFromVerse2VOrder = dbCorrection.BreakFromVerse2VOrder,
                    BreakFromVerse2VOrderResult = dbCorrection.BreakFromVerse2VOrderResult,
                    BreakFromVerse3VOrder = dbCorrection.BreakFromVerse3VOrder,
                    BreakFromVerse3VOrderResult = dbCorrection.BreakFromVerse3VOrderResult,
                    BreakFromVerse4VOrder = dbCorrection.BreakFromVerse4VOrder,
                    BreakFromVerse4VOrderResult = dbCorrection.BreakFromVerse4VOrderResult,
                    Note = dbCorrection.Note,
                    Date = dbCorrection.Date,
                    Reviewed = dbCorrection.Reviewed,
                    ReviewNote = dbCorrection.ReviewNote,
                    ReviewDate = dbCorrection.ReviewDate,
                    UserNickname = string.IsNullOrEmpty(dbCorrection.User.NickName) ? dbCorrection.User.Id.ToString() : dbCorrection.User.NickName
                }
                );
        }

        /// <summary>
        /// send a correction for a section
        /// </summary>
        /// <param name="correction"></param>
        /// <returns></returns>
        public async Task<RServiceResult<GanjoorPoemSectionCorrectionViewModel>> SuggestPoemSectionCorrection(GanjoorPoemSectionCorrectionViewModel correction)
        {
            try
            {
                var preCorrections = await _context.GanjoorPoemSectionCorrections
                .Where(c => c.UserId == correction.UserId && c.SectionId == correction.SectionId && c.Reviewed == false)
                .ToListAsync();


                GanjoorPoemSectionCorrection dbCorrection = new GanjoorPoemSectionCorrection()
                {
                    SectionId = correction.SectionId,
                    UserId = correction.UserId,
                    Rhythm = correction.Rhythm,
                    RhythmResult = CorrectionReviewResult.NotReviewed,
                    BreakFromVerse1VOrder = correction.BreakFromVerse1VOrder,
                    BreakFromVerse1VOrderResult = CorrectionReviewResult.NotReviewed,
                    BreakFromVerse2VOrder = correction.BreakFromVerse2VOrder,
                    BreakFromVerse2VOrderResult = CorrectionReviewResult.NotReviewed,
                    BreakFromVerse3VOrder = correction.BreakFromVerse3VOrder,
                    BreakFromVerse3VOrderResult = CorrectionReviewResult.NotReviewed,
                    BreakFromVerse4VOrder = correction.BreakFromVerse4VOrder,
                    BreakFromVerse4VOrderResult = CorrectionReviewResult.NotReviewed,
                    Note = correction.Note,
                    Date = DateTime.Now,
                    Reviewed = false,
                    AffectedThePoem = false,
                };
                _context.GanjoorPoemSectionCorrections.Add(dbCorrection);
                await _context.SaveChangesAsync();
                correction.Id = dbCorrection.Id;

                if (preCorrections.Count > 0)
                {
                    _context.GanjoorPoemSectionCorrections.RemoveRange(preCorrections);
                    await _context.SaveChangesAsync();
                }

                return new RServiceResult<GanjoorPoemSectionCorrectionViewModel>(correction);
            }
            catch (Exception exp)
            {
                return new RServiceResult<GanjoorPoemSectionCorrectionViewModel>(null, exp.ToString());
            }
        }

        /// <summary>
        /// delete unreviewed user corrections for a poem section
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="sectionId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<bool>> DeletePoemSectionCorrections(Guid userId, int sectionId)
        {
            var preCorrections = await _context.GanjoorPoemSectionCorrections
                .Where(c => c.UserId == userId && c.SectionId == sectionId && c.Reviewed == false)
                .ToListAsync();
            if (preCorrections.Count > 0)
            {
                _context.GanjoorPoemSectionCorrections.RemoveRange(preCorrections);
                await _context.SaveChangesAsync();
            }
            return new RServiceResult<bool>(true);
        }

        /// <summary>
        /// get next unreviewed correction for a poem section
        /// </summary>
        /// <param name="skip"></param>
        /// <returns></returns>
        public async Task<RServiceResult<GanjoorPoemSectionCorrectionViewModel>> GetNextUnreviewedPoemSectionCorrection(int skip)
        {
            var dbCorrection = await _context.GanjoorPoemSectionCorrections.AsNoTracking().Include(c => c.User)
                .Where(c => c.Reviewed == false)
                .OrderBy(c => c.Id)
                .Skip(skip)
                .FirstOrDefaultAsync();

            if (dbCorrection == null)
                return new RServiceResult<GanjoorPoemSectionCorrectionViewModel>(null);

            return new RServiceResult<GanjoorPoemSectionCorrectionViewModel>
                (
                new GanjoorPoemSectionCorrectionViewModel()
                {
                    Id = dbCorrection.Id,
                    SectionId = dbCorrection.SectionId,
                    UserId = dbCorrection.UserId,
                    Rhythm = dbCorrection.Rhythm,
                    OriginalRhythm = dbCorrection.OriginalRhythm,
                    RhythmResult = dbCorrection.RhythmResult,
                    Rhythm2 = dbCorrection.Rhythm2,
                    OriginalRhythm2 = dbCorrection.OriginalRhythm2,
                    RhythmResult2 = dbCorrection.RhythmResult2,
                    BreakFromVerse1VOrder = dbCorrection.BreakFromVerse1VOrder,
                    BreakFromVerse1VOrderResult = dbCorrection.BreakFromVerse1VOrderResult,
                    BreakFromVerse2VOrder = dbCorrection.BreakFromVerse2VOrder,
                    BreakFromVerse2VOrderResult = dbCorrection.BreakFromVerse2VOrderResult,
                    BreakFromVerse3VOrder = dbCorrection.BreakFromVerse3VOrder,
                    BreakFromVerse3VOrderResult = dbCorrection.BreakFromVerse3VOrderResult,
                    BreakFromVerse4VOrder = dbCorrection.BreakFromVerse4VOrder,
                    BreakFromVerse4VOrderResult = dbCorrection.BreakFromVerse4VOrderResult,
                    Note = dbCorrection.Note,
                    Date = dbCorrection.Date,
                    Reviewed = dbCorrection.Reviewed,
                    ReviewNote = dbCorrection.ReviewNote,
                    ReviewDate = dbCorrection.ReviewDate,
                    UserNickname = string.IsNullOrEmpty(dbCorrection.User.NickName) ? dbCorrection.User.Id.ToString() : dbCorrection.User.NickName
                }
                );
        }

        /// <summary>
        /// get section correction by id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<RServiceResult<GanjoorPoemSectionCorrectionViewModel>> GetSectionCorrectionById(int id)
        {
            var dbCorrection = await _context.GanjoorPoemSectionCorrections.AsNoTracking().Include(c => c.User)
                .Where(c => c.Id == id)
                .FirstOrDefaultAsync();

            if (dbCorrection == null)
                return new RServiceResult<GanjoorPoemSectionCorrectionViewModel>(null);

            return new RServiceResult<GanjoorPoemSectionCorrectionViewModel>
                (
                new GanjoorPoemSectionCorrectionViewModel()
                {
                    Id = dbCorrection.Id,
                    SectionId = dbCorrection.SectionId,
                    UserId = dbCorrection.UserId,
                    Rhythm = dbCorrection.Rhythm,
                    OriginalRhythm = dbCorrection.OriginalRhythm,
                    RhythmResult = dbCorrection.RhythmResult,
                    Rhythm2 = dbCorrection.Rhythm2,
                    OriginalRhythm2 = dbCorrection.OriginalRhythm2,
                    RhythmResult2 = dbCorrection.RhythmResult2,
                    BreakFromVerse1VOrder = dbCorrection.BreakFromVerse1VOrder,
                    BreakFromVerse1VOrderResult = dbCorrection.BreakFromVerse1VOrderResult,
                    BreakFromVerse2VOrder = dbCorrection.BreakFromVerse2VOrder,
                    BreakFromVerse2VOrderResult = dbCorrection.BreakFromVerse2VOrderResult,
                    BreakFromVerse3VOrder = dbCorrection.BreakFromVerse3VOrder,
                    BreakFromVerse3VOrderResult = dbCorrection.BreakFromVerse3VOrderResult,
                    BreakFromVerse4VOrder = dbCorrection.BreakFromVerse4VOrder,
                    BreakFromVerse4VOrderResult = dbCorrection.BreakFromVerse4VOrderResult,
                    Note = dbCorrection.Note,
                    Date = dbCorrection.Date,
                    Reviewed = dbCorrection.Reviewed,
                    ReviewNote = dbCorrection.ReviewNote,
                    ReviewDate = dbCorrection.ReviewDate,
                    UserNickname = string.IsNullOrEmpty(dbCorrection.User.NickName) ? dbCorrection.User.Id.ToString() : dbCorrection.User.NickName
                }
                );
        }

        /// <summary>
        /// unreview poem section correction count
        /// </summary>
        /// <returns></returns>
        public async Task<RServiceResult<int>> GetUnreviewedPoemSectionCorrectionCount()
        {
            return new RServiceResult<int>(await _context.GanjoorPoemSectionCorrections.AsNoTracking()
                .Where(c => c.Reviewed == false)
                .CountAsync());
        }
    }
}