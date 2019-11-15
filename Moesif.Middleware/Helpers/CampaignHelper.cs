using System;
using System.Collections.Generic;
using Moesif.Api.Models;

namespace Moesif.Middleware.Helpers
{
    public class CampaignHelper
    {
        // Create CampaignModel
        public CampaignModel createCampaignModel(Dictionary<string, object> campaign)
        {
            var utmSource = new object();
            var utmMedium = new object();
            var utmCampaign = new object();
            var utmTerm = new object();
            var utmContent = new object();
            var referrer = new object();
            var referringDomain = new object();
            var gclid = new object();

            var campaignModel = new CampaignModel()
            {
                UtmSource = campaign.TryGetValue("utm_source", out utmSource) ? utmSource.ToString() : null,
                UtmMedium = campaign.TryGetValue("utm_medium", out utmMedium) ? utmMedium.ToString() : null,
                UtmCampaign = campaign.TryGetValue("utm_campaign", out utmCampaign) ? utmCampaign.ToString() : null,
                UtmTerm = campaign.TryGetValue("utm_term", out utmTerm) ? utmTerm.ToString() : null,
                UtmContent = campaign.TryGetValue("utm_content", out utmContent) ? utmContent.ToString() : null,
                Referrer = campaign.TryGetValue("referrer", out referrer) ? referrer.ToString() : null,
                ReferringDomain = campaign.TryGetValue("referring_domain", out referringDomain) ? referringDomain.ToString() : null,
                Gclid = campaign.TryGetValue("gclid", out gclid) ? gclid.ToString() : null
            };
            return campaignModel;
        }
    }
}
