﻿using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using GanjooRazor.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RMuseum.Models.Ganjoor;
using RMuseum.Models.Ganjoor.ViewModels;

namespace GanjooRazor.Areas.Admin.Pages
{
    [IgnoreAntiforgeryToken(Order = 1001)]
    public class CatTransModel : PageModel
    {
        /// <summary>
        /// last message
        /// </summary>
        public string LastMessage { get; set; }

        /// <summary>
        /// poems
        /// </summary>
        public GanjoorDuplicateViewModel[] Poems { get; set; }


        /// <summary>
        /// cat id
        /// </summary>
        public int CatId { get; set; }

        /// <summary>
        /// dest cat Id
        /// </summary>
        [BindProperty]
        public int DestCatId { get; set; }

        private async Task<bool> _GetCatDuplicatesAsync()
        {
            if (string.IsNullOrEmpty(Request.Query["id"]))
            {
                LastMessage = "شناسهٔ بخش مشخص نیست.";
                return false;
            }
            CatId = int.Parse(Request.Query["id"]);
            using (HttpClient secureClient = new HttpClient())
            {
                if (await GanjoorSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    HttpResponseMessage response = await secureClient.GetAsync($"{APIRoot.Url}/api/ganjoor/duplicates/{CatId}");
                    if (!response.IsSuccessStatusCode)
                    {
                        LastMessage = JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync());
                        return false;
                    }
                    else
                    {
                        Poems = JsonConvert.DeserializeObject<GanjoorDuplicateViewModel[]>(await response.Content.ReadAsStringAsync());

                        if(!string.IsNullOrEmpty(Request.Query["empty"]))
                        {
                            Poems = Poems.Where(p => p.DestPoemId == null).ToArray();
                        }
                    }
                }
                else
                {
                    LastMessage = "لطفا از گنجور خارج و مجددا به آن وارد شوید.";
                    return false;
                }
            }
            return true;
        }
        public async Task<IActionResult> OnGetAsync()
        {
            if (string.IsNullOrEmpty(Request.Cookies["Token"]))
                return Redirect("/");

            await _GetCatDuplicatesAsync();


            return Page();
        }

        public async Task<IActionResult> OnPostFindDuplicatesAsync()
        {
            CatId = int.Parse(Request.Query["id"]);
            using (HttpClient secureClient = new HttpClient())
            {
                if (await GanjoorSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    HttpResponseMessage response = await secureClient.PostAsync($"{APIRoot.Url}/api/ganjoor/duplicates/{CatId}/{DestCatId}?hardTry=true", null);
                    if (!response.IsSuccessStatusCode)
                    {
                        LastMessage = JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync());
                        return Page();
                    }
                    else
                    {
                        LastMessage = "فرایند شروع شد.";
                        return Page();
                    }
                }
                else
                {
                    LastMessage = "لطفا از گنجور خارج و مجددا به آن وارد شوید.";
                    return Page();
                }
            }
        }

        public async Task<IActionResult> OnPostFinalizeAsync()
        {
            CatId = int.Parse(Request.Query["id"]);
            using (HttpClient secureClient = new HttpClient())
            {
                if (await GanjoorSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    HttpResponseMessage response = await secureClient.PutAsync($"{APIRoot.Url}/api/ganjoor/duplicates/finish/{CatId}/{DestCatId}", null);
                    if (!response.IsSuccessStatusCode)
                    {
                        LastMessage = JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync());
                        return Page();
                    }
                    else
                    {
                        LastMessage = "فرایند شروع شد.";
                        return Page();
                    }
                }
                else
                {
                    LastMessage = "لطفا از گنجور خارج و مجددا به آن وارد شوید.";
                    return Page();
                }
            }
        }

        public async Task<IActionResult> OnDeleteAsync(int id)
        {
            using (HttpClient secureClient = new HttpClient())
            {
                if (await GanjoorSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    HttpResponseMessage response = await secureClient.DeleteAsync($"{APIRoot.Url}/api/ganjoor/duplicates/{id}");
                    if (!response.IsSuccessStatusCode)
                    {
                        LastMessage = JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync());
                        return new BadRequestObjectResult(LastMessage);
                    }
                    else
                    {
                        return new OkResult();
                    }
                }
                else
                {
                    return new BadRequestObjectResult("لطفا از گنجور خارج و مجددا به آن وارد شوید.");
                }
            }
        }
    }
        
}
