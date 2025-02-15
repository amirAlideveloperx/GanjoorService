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
        /// moderate poem correction
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="moderation"></param>
        /// <returns></returns>
        public async Task<RServiceResult<GanjoorPoemCorrectionViewModel>> ModeratePoemCorrection(Guid userId,
            GanjoorPoemCorrectionViewModel moderation)
        {
            var dbCorrection = await _context.GanjoorPoemCorrections.Include(c => c.VerseOrderText).Include(c => c.User)
                .Where(c => c.Id == moderation.Id)
                .FirstOrDefaultAsync();

            if (dbCorrection == null)
                return new RServiceResult<GanjoorPoemCorrectionViewModel>(null);

            if (!string.IsNullOrEmpty(dbCorrection.Rhythm3) || !string.IsNullOrEmpty(dbCorrection.Rhythm4))
                return new RServiceResult<GanjoorPoemCorrectionViewModel>(null, "انتساب وزن سوم و چهارم هنوز پیاده‌سازی نشده است.");

            dbCorrection.ReviewerUserId = userId;
            dbCorrection.ReviewDate = DateTime.Now;
            dbCorrection.ApplicationOrder = await _context.GanjoorPoemCorrections.Where(c => c.Reviewed).AnyAsync() ? 1 + await _context.GanjoorPoemCorrections.Where(c => c.Reviewed).MaxAsync(c => c.ApplicationOrder) : 1;
            
            dbCorrection.AffectedThePoem = false;
            dbCorrection.ReviewNote = moderation.ReviewNote;

            var dbPoem = await _context.GanjoorPoems.Include(p => p.GanjoorMetre).Where(p => p.Id == moderation.PoemId).SingleOrDefaultAsync();
            var dbPage = await _context.GanjoorPages.Where(p => p.Id == moderation.PoemId).SingleOrDefaultAsync();
            var sections = await _context.GanjoorPoemSections.Include(s => s.GanjoorMetre).Where(s => s.PoemId == moderation.PoemId).OrderBy(s => s.SectionType).ThenBy(s => s.Index).ToListAsync();
            foreach (var section in sections)
            {
                section.OldGanjoorMetreId = section.GanjoorMetreId;
                section.OldRhymeLetters = section.RhymeLetters;
            }
            //beware: items consisting only of paragraphs have no main setion (mainSection in the following line can legitimately become null)
            var mainSection = sections.FirstOrDefault(s => s.SectionType == PoemSectionType.WholePoem && s.VerseType == VersePoemSectionType.First);

            GanjoorPageSnapshot snapshot = new GanjoorPageSnapshot()
            {
                GanjoorPageId = dbPage.Id,
                MadeObsoleteByUserId = userId,
                RecordDate = DateTime.Now,
                Note = $"بررسی پیشنهاد ویرایش با شناسهٔ {dbCorrection.Id}",
                Title = dbPoem.Title,
                UrlSlug = dbPoem.UrlSlug,
                HtmlText = dbPoem.HtmlText,
                SourceName = dbPoem.SourceName,
                SourceUrlSlug = dbPoem.SourceUrlSlug,
                Rhythm = mainSection == null || mainSection.GanjoorMetre == null ? null : mainSection.GanjoorMetre.Rhythm,
                RhymeLetters = mainSection == null ? null : mainSection.RhymeLetters,
                OldTag = dbPoem.OldTag,
                OldTagPageUrl = dbPoem.OldTagPageUrl
            };
            _context.GanjoorPageSnapshots.Add(snapshot);
            await _context.SaveChangesAsync();

            bool updatePoem = false;
            if (dbCorrection.Title != null)
            {
                if (moderation.Result == CorrectionReviewResult.NotReviewed)
                    return new RServiceResult<GanjoorPoemCorrectionViewModel>(null, "تغییرات عنوان بررسی نشده است.");
                dbCorrection.Result = moderation.Result;
                
                if (dbCorrection.Result == CorrectionReviewResult.Approved)
                {
                    dbCorrection.AffectedThePoem = true;
                    dbPoem.Title = moderation.Title.Replace("ۀ", "هٔ").Replace("ك", "ک");
                    dbPage.Title = dbPoem.Title;
                    if(dbPage.ParentId != null)
                    {
                        GanjoorPage parent = await _context.GanjoorPages.AsNoTracking().Where(p => p.Id == dbPage.ParentId).SingleAsync();
                        dbPage.FullTitle = parent.FullTitle + " » " + dbPage.Title;
                        dbPoem.FullTitle = dbPage.FullTitle;
                    }
                    updatePoem = true;
                }
            }

            
            int maxSections = sections.Count == 0 ? 0 : sections.Max(s => s.Index);

            var poemVerses = await _context.GanjoorVerses.Where(p => p.PoemId == dbCorrection.PoemId).OrderBy(v => v.VOrder).ToListAsync();

            if (moderation.VerseOrderText.Length != dbCorrection.VerseOrderText.Count)
                return new RServiceResult<GanjoorPoemCorrectionViewModel>(null, "moderation.VerseOrderText.Length != dbCorrection.VerseOrderText.Count");
            
            var modifiedVerses = new List<GanjoorVerse>();

            foreach (var moderatedVerse in moderation.VerseOrderText)
            {
                if (moderatedVerse.MarkForDelete)
                    continue;
                if (moderatedVerse.Result == CorrectionReviewResult.NotReviewed)
                    return new RServiceResult<GanjoorPoemCorrectionViewModel>(null, $"تغییرات مصرع {moderatedVerse.VORder} بررسی نشده است.");
                var dbVerse = dbCorrection.VerseOrderText.Where(c => c.VORder == moderatedVerse.VORder).Single();
                dbVerse.Result = moderatedVerse.Result;
                dbVerse.ReviewNote = moderatedVerse.ReviewNote;
                if(dbVerse.VersePosition != null)
                {
                    if(moderatedVerse.VersePositionResult == CorrectionReviewResult.NotReviewed)
                        return new RServiceResult<GanjoorPoemCorrectionViewModel>(null, $"تغییرات نوع مصرع {moderatedVerse.VORder} بررسی نشده است.");
                    dbVerse.VersePositionResult = moderatedVerse.VersePositionResult;
                }
                if (dbVerse.Result == CorrectionReviewResult.Approved || dbVerse.VersePositionResult == CorrectionReviewResult.Approved)
                {
                    dbCorrection.AffectedThePoem = true;
                    var poemVerse = poemVerses.Where(v => v.VOrder == moderatedVerse.VORder).Single();
                    if(dbVerse.Result == CorrectionReviewResult.Approved)
                        poemVerse.Text = moderatedVerse.Text.Replace("ۀ", "هٔ").Replace("ك", "ک");
                    if (dbVerse.VersePositionResult == CorrectionReviewResult.Approved)
                        poemVerse.VersePosition = (VersePosition)dbVerse.VersePosition;
                    modifiedVerses.Add(poemVerse);
                }
            }


            if (modifiedVerses.Count > 0)
            {
                _context.UpdateRange(modifiedVerses);
                dbPoem.HtmlText = PrepareHtmlText(poemVerses);
                dbPoem.PlainText = PreparePlainText(poemVerses);
                dbPage.HtmlText = dbPoem.HtmlText;
                updatePoem = true;
            }

            
            if (updatePoem)
            {
                _context.Update(dbPoem);
                _context.Update(dbPage);
            }

            bool versesDeleted = false;
            foreach (var moderatedVerse in moderation.VerseOrderText.Where(v => v.MarkForDelete == true).ToList())
            {
                if (moderatedVerse.MarkForDeleteResult == CorrectionReviewResult.NotReviewed)
                    return new RServiceResult<GanjoorPoemCorrectionViewModel>(null, $"نتیجهٔ پیشنهاد حذف مصرع {moderatedVerse.VORder} بررسی نشده است.");
                var dbVerse = dbCorrection.VerseOrderText.Where(c => c.VORder == moderatedVerse.VORder).Single();
                dbVerse.MarkForDeleteResult = moderatedVerse.MarkForDeleteResult;
                dbVerse.ReviewNote = moderatedVerse.ReviewNote;
                if (dbVerse.MarkForDeleteResult == CorrectionReviewResult.Approved)
                {
                    dbVerse.OriginalText = dbVerse.Text;
                    dbCorrection.AffectedThePoem = true;
                    versesDeleted = true;
                    updatePoem = true;

                    var poemVerse = poemVerses.Where(v => v.VOrder == moderatedVerse.VORder).Single();
                    _context.Remove(poemVerse);
                }
            }

            if (versesDeleted)
            {
                var undeletedPoemVerss = poemVerses.Where(v => !moderation.VerseOrderText.Any(mv => mv.VORder == v.VOrder && mv.MarkForDelete == true )).ToList();
                for (int vOrder = 1; vOrder < undeletedPoemVerss.Count; vOrder++)
                {
                    if (undeletedPoemVerss[vOrder - 1].VOrder != vOrder)
                    {
                        undeletedPoemVerss[vOrder - 1].VOrder = vOrder;
                    }
                }

                int cIndex = -1;
                foreach (var verse in undeletedPoemVerss)
                {
                    if (verse.VersePosition != VersePosition.Left && verse.VersePosition != VersePosition.CenteredVerse2 && verse.VersePosition != VersePosition.Comment)
                        cIndex++;
                    if (verse.VersePosition != VersePosition.Comment)
                    {
                        verse.CoupletIndex = cIndex;
                    }
                    else
                    {
                        verse.CoupletIndex = null;
                    }
                }
                _context.UpdateRange(undeletedPoemVerss);
                dbPoem.HtmlText = PrepareHtmlText(undeletedPoemVerss);
                dbPoem.PlainText = PreparePlainText(undeletedPoemVerss);
                dbPage.HtmlText = dbPoem.HtmlText;
                updatePoem = true;
                poemVerses = undeletedPoemVerss;
            }



            if (modifiedVerses.Count > 0 || versesDeleted)
            {
                foreach (var section in sections)
                {
                    var sectionVerses = poemVerses.Where(v =>
                            (section.VerseType == VersePoemSectionType.First && v.SectionIndex1 == section.Index)
                            ||
                            (section.VerseType == VersePoemSectionType.Second && v.SectionIndex2 == section.Index)
                            ||
                            (section.VerseType == VersePoemSectionType.Third && v.SectionIndex3 == section.Index)
                            ||
                            (section.VerseType == VersePoemSectionType.Forth && v.SectionIndex4 == section.Index)
                            ).OrderBy(v => v.VOrder).ToList();
                    if (sectionVerses.Any(v => modifiedVerses.Contains(v)))
                    {
                        section.HtmlText = PrepareHtmlText(sectionVerses);
                        section.PlainText = PreparePlainText(sectionVerses);
                        section.Modified = true;
                        section.OldRhymeLetters = section.RhymeLetters ?? "";
                        try
                        {
                            var newRhyme = LanguageUtils.FindRhyme(sectionVerses);
                            if (!string.IsNullOrEmpty(newRhyme.Rhyme) &&
                                (string.IsNullOrEmpty(section.RhymeLetters) || (!string.IsNullOrEmpty(section.RhymeLetters) && newRhyme.Rhyme.Length > section.RhymeLetters.Length))
                                )
                            {
                                
                                if (section.OldRhymeLetters != newRhyme.Rhyme)
                                {
                                    section.RhymeLetters = newRhyme.Rhyme;
                                }
                            }
                        }
                        catch
                        {

                        }
                        _context.Update(section);
                    }
                }
            }

            

            if (dbCorrection.Rhythm != null)
            {
                if (moderation.RhythmResult == CorrectionReviewResult.NotReviewed)
                    return new RServiceResult<GanjoorPoemCorrectionViewModel>(null, "تغییرات وزن بررسی نشده است.");
                dbCorrection.RhythmResult = moderation.RhythmResult;
                if (dbCorrection.RhythmResult == CorrectionReviewResult.Approved)
                {
                    if (mainSection == null)
                    {
                        return new RServiceResult<GanjoorPoemCorrectionViewModel>(null, "شعر فاقد بخش اصلی برای ذخیرهٔ وزن است.");
                    }
                    if (poemVerses.Where(v => v.VersePosition == VersePosition.Paragraph).Any())
                    {
                        return new RServiceResult<GanjoorPoemCorrectionViewModel>(null, "امکان انتساب وزن به متون مخلوط از طریق ویرایشگر کاربر وجود ندارد.");
                    }
                    if (sections.Where(s => s.SectionType == PoemSectionType.WholePoem && s.VerseType == VersePoemSectionType.First).Count() > 1)
                    {
                        return new RServiceResult<GanjoorPoemCorrectionViewModel>(null, "امکان انتساب وزن به متون حاوی بیش از یک شعر از طریق ویرایشگر کاربر وجود ندارد.");
                    }

                    dbCorrection.AffectedThePoem = true;
                    mainSection.OldGanjoorMetreId = mainSection.GanjoorMetreId;
                    if (moderation.Rhythm == "")
                    {
                        mainSection.GanjoorMetreId = null;

                        if(sections.Any(s => s.SectionType == PoemSectionType.WholePoem && s.VerseType != VersePoemSectionType.First && s.GanjoorMetreId != null))
                        {
                            return new RServiceResult<GanjoorPoemCorrectionViewModel>(null, "حذف وزن اصلی در حالی که وزنهای دیگری منتسب شده امکان ندارد.");
                        }
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
                        mainSection.GanjoorMetreId = metre.Id;
                    }

                    mainSection.Modified = mainSection.OldGanjoorMetreId != mainSection.GanjoorMetreId;

                    foreach (var section in sections.Where(s => s.GanjoorMetreRefSectionIndex == mainSection.Index))
                    {
                        section.GanjoorMetreId = mainSection.GanjoorMetreId;
                        section.Modified = mainSection.Modified;
                    }
                }
            }

            if (dbCorrection.Rhythm2 != null)
            {
                if (moderation.Rhythm2Result == CorrectionReviewResult.NotReviewed)
                    return new RServiceResult<GanjoorPoemCorrectionViewModel>(null, "تغییرات وزن دوم بررسی نشده است.");
                dbCorrection.Rhythm2Result = moderation.Rhythm2Result;
                if (dbCorrection.Rhythm2Result == CorrectionReviewResult.Approved)
                {
                    if (mainSection == null)
                    {
                        return new RServiceResult<GanjoorPoemCorrectionViewModel>(null, "شعر فاقد بخش اصلی برای ذخیرهٔ وزن است.");
                    }
                    if (poemVerses.Where(v => v.VersePosition == VersePosition.Paragraph).Any())
                    {
                        return new RServiceResult<GanjoorPoemCorrectionViewModel>(null, "امکان انتساب وزن به متون مخلوط از طریق ویرایشگر کاربر وجود ندارد.");
                    }
                    if (sections.Where(s => s.SectionType == PoemSectionType.WholePoem && s.VerseType == VersePoemSectionType.First).Count() > 1)
                    {
                        return new RServiceResult<GanjoorPoemCorrectionViewModel>(null, "امکان انتساب وزن به متون حاوی بیش از یک شعر از طریق ویرایشگر کاربر وجود ندارد.");
                    }
                    if (sections.Where(s => s.SectionType == PoemSectionType.WholePoem && s.VerseType == VersePoemSectionType.Second).Count() > 1)
                    {
                        return new RServiceResult<GanjoorPoemCorrectionViewModel>(null, "امکان انتساب وزن به متون حاوی بیش از یک شعر از طریق ویرایشگر کاربر وجود ندارد.");
                    }

                    dbCorrection.AffectedThePoem = true;
                    var secondMetreSection = sections.FirstOrDefault(s => s.SectionType == PoemSectionType.WholePoem && s.VerseType == VersePoemSectionType.Second);

                    if (moderation.Rhythm2 == "")
                    {
                        if (secondMetreSection != null)
                        {
                            if (sections.Any(s => s.SectionType == PoemSectionType.WholePoem && s.VerseType != VersePoemSectionType.First && s.VerseType != VersePoemSectionType.Second && s.GanjoorMetreId != null))
                            {
                                return new RServiceResult<GanjoorPoemCorrectionViewModel>(null, "حذف وزن دوم در حالی که وزنهای سوم یا چهارم منتسب شده امکان ندارد.");
                            }

                            //the following block enusres updating related sections near the end of the method to work correctly (_UpdateRelatedSections):
                            secondMetreSection.OldGanjoorMetreId = secondMetreSection.GanjoorMetreId;
                            secondMetreSection.OldRhymeLetters = secondMetreSection.RhymeLetters;
                            secondMetreSection.GanjoorMetreId = null;
                            secondMetreSection.RhymeLetters = null;
                            secondMetreSection.Modified = true;

                            foreach (var section in sections.Where(s => s.GanjoorMetreRefSectionIndex == secondMetreSection.Index))
                            {
                                _context.Remove(section);
                                section.OldGanjoorMetreId = section.GanjoorMetreId;
                                section.OldRhymeLetters = section.RhymeLetters;
                                section.GanjoorMetreId = null;
                                section.RhymeLetters = null;
                                section.Modified = true;
                            }
                            _context.Remove(secondMetreSection);

                            foreach (var verse in poemVerses)
                            {
                                verse.SectionIndex2 = null;
                                verse.SectionIndex3 = null;
                                _context.Update(verse);
                            }
                        }
                    }
                    else
                    {
                        if (secondMetreSection == null)
                        {
                            maxSections++;
                            VersePoemSectionType newSectionType = sections.Where(s => s.VerseType == VersePoemSectionType.Second).Any() ? VersePoemSectionType.Third : VersePoemSectionType.Second;
                            secondMetreSection = new GanjoorPoemSection()
                            {
                                PoemId = dbPoem.Id,
                                PoetId = mainSection.PoetId,
                                SectionType = PoemSectionType.WholePoem,
                                VerseType = newSectionType,
                                Index = maxSections,
                                Number = maxSections + 1,
                                GanjoorMetreId = null,
                                RhymeLetters = mainSection.RhymeLetters,
                                HtmlText = mainSection.HtmlText,
                                PlainText = mainSection.PlainText,
                                PoemFormat = mainSection.PoemFormat,
                            };
                            _context.Add(secondMetreSection);
                            sections.Add(secondMetreSection);

                            foreach (var verse in poemVerses)
                            {
                                if(newSectionType == VersePoemSectionType.Second)
                                    verse.SectionIndex2 = secondMetreSection.Index;
                                else
                                    verse.SectionIndex3 = secondMetreSection.Index;
                            }

                            foreach (var secondLevelSections in sections.Where(s => s.SectionType != PoemSectionType.WholePoem && s.VerseType == VersePoemSectionType.Second).OrderBy(s => s.Index))
                            {
                                maxSections++;
                                var newSection = new GanjoorPoemSection()
                                {
                                    PoemId = secondLevelSections.PoemId,
                                    PoetId = secondLevelSections.PoetId,
                                    SectionType = secondLevelSections.SectionType,
                                    VerseType =  VersePoemSectionType.Forth,
                                    Index = maxSections,
                                    Number = maxSections + 1,
                                    GanjoorMetreId = null,
                                    RhymeLetters = secondLevelSections.RhymeLetters,
                                    HtmlText = secondLevelSections.HtmlText,
                                    PlainText = secondLevelSections.PlainText,
                                    PoemFormat = secondLevelSections.PoemFormat,
                                    GanjoorMetreRefSectionIndex = secondMetreSection.Index,
                                };
                                _context.Add(newSection);
                                sections.Add(newSection);

                                foreach (var verse in poemVerses)
                                {
                                    if(verse.SectionIndex2 == secondLevelSections.Index)
                                    {
                                        verse.SectionIndex4 = newSection.Index;
                                    }
                                }
                            }
                            _context.UpdateRange(poemVerses);
                            await _context.SaveChangesAsync();
                        }
                        secondMetreSection.OldGanjoorMetreId = secondMetreSection.GanjoorMetreId;

                        var metre = await _context.GanjoorMetres.AsNoTracking().Where(m => m.Rhythm == moderation.Rhythm2).SingleOrDefaultAsync();
                        if (metre == null)
                        {
                            metre = new GanjoorMetre()
                            {
                                Rhythm = moderation.Rhythm2,
                                VerseCount = 0
                            };
                            _context.GanjoorMetres.Add(metre);
                            await _context.SaveChangesAsync();
                        }
                        secondMetreSection.GanjoorMetreId = metre.Id;
                        secondMetreSection.Modified = secondMetreSection.GanjoorMetreId != secondMetreSection.OldGanjoorMetreId;
                        _context.Update(secondMetreSection);

                        foreach (var section in sections.Where(s => s.GanjoorMetreRefSectionIndex == secondMetreSection.Index))
                        {
                            section.GanjoorMetreId = secondMetreSection.GanjoorMetreId;
                            section.Modified = secondMetreSection.Modified;
                            _context.Update(section);
                        }
                    }
                }
            }

            dbCorrection.Reviewed = true;
            _context.GanjoorPoemCorrections.Update(dbCorrection);
            await _context.SaveChangesAsync();

            await _notificationService.PushNotification(dbCorrection.UserId,
                               "بررسی ویرایش پیشنهادی شما",
                               $"با سپاس از زحمت و همت شما ویرایش پیشنهادیتان برای <a href=\"{dbPoem.FullUrl}\" target=\"_blank\">{dbPoem.FullTitle}</a> بررسی شد.{Environment.NewLine}" +
                               $"جهت مشاهدهٔ نتیجهٔ بررسی در میز کاربری خود بخش «<a href=\"/User/Edits\">ویرایش‌های من</a>» را مشاهده بفرمایید.{Environment.NewLine}"
                               );

            foreach (var section in sections)
            {
                if(section.Modified)
                {
                    if(section.OldGanjoorMetreId != section.GanjoorMetreId || section.OldRhymeLetters != section.RhymeLetters)
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

            return new RServiceResult<GanjoorPoemCorrectionViewModel>(moderation);
        }
    }
}
