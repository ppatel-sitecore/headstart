using System;
using System.Linq;
using OrderCloud.SDK;
using Headstart.Models;
using Headstart.Common;
using Sitecore.Diagnostics;
using Headstart.Models.Misc;
using System.Threading.Tasks;
using Sitecore.Foundation.SitecoreExtensions.Extensions;
using Sitecore.Foundation.SitecoreExtensions.MVC.Extenstions;

namespace Headstart.API.Commands
{
    public interface IHSBuyerCommand
    {
        Task<SuperHSBuyer> Create(SuperHSBuyer buyer);
        Task<SuperHSBuyer> Create(SuperHSBuyer buyer, string accessToken, IOrderCloudClient oc);
        Task<SuperHSBuyer> Get(string buyerID);
        Task<SuperHSBuyer> Update(string buyerID, SuperHSBuyer buyer);
    }

    public class HSBuyerCommand : IHSBuyerCommand
    {
        private readonly IOrderCloudClient _oc;
        private readonly AppSettings _settings; 
        private WebConfigSettings _webConfigSettings = WebConfigSettings.Instance;

        /// <summary>
        /// The IOC based constructor method for the HSBuyerCommand class object with Dependency Injection
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="oc"></param>
        public HSBuyerCommand(AppSettings settings, IOrderCloudClient oc)
        {
            try
            {
                _settings = settings;
                _oc = oc;
            }
            catch (Exception ex)
            {
                LogExt.LogException(_webConfigSettings.AppLogFileKey, Helpers.GetMethodName(), $@"{LoggingNotifications.GetGeneralLogMessagePrefixKey()}", ex.Message, ex.StackTrace, this, true);
            }
        }

        /// <summary>
        /// Public re-usable Create task method for creating a SuperHSBuyer
        /// </summary>
        /// <param name="superBuyer"></param>
        /// <returns>The newly created SuperHSBuyer object</returns>
        public async Task<SuperHSBuyer> Create(SuperHSBuyer superBuyer)
        {
            var resp = new SuperHSBuyer();
            try
            {
                resp = await Create(superBuyer, null, _oc);
            }
            catch (Exception ex)
            {
                LogExt.LogException(_webConfigSettings.AppLogFileKey, Helpers.GetMethodName(), $@"{LoggingNotifications.GetGeneralLogMessagePrefixKey()}", ex.Message, ex.StackTrace, this, true);
            }
            return resp;
        }

        /// <summary>
        /// Public re-usable Create task method for creating a SuperHSBuyer
        /// </summary>
        /// <param name="superBuyer"></param>
        /// <param name="accessToken"></param>
        /// <param name="oc"></param>
        /// <returns>The newly created SuperHSBuyer object</returns>
        public async Task<SuperHSBuyer> Create(SuperHSBuyer superBuyer, string accessToken, IOrderCloudClient oc)
        {
            var resp = new SuperHSBuyer();
            try
            {
                var createdImpersonationConfig = new ImpersonationConfig();
                var createdBuyer = await CreateBuyerAndRelatedFunctionalResources(superBuyer.Buyer, accessToken, oc);
                var createdMarkup = await CreateMarkup(superBuyer.Markup, createdBuyer.ID, accessToken, oc);
                if (superBuyer?.ImpersonationConfig != null)
                {
                    createdImpersonationConfig = await SaveImpersonationConfig(superBuyer.ImpersonationConfig, createdBuyer.ID, accessToken, oc);
                }
                return new SuperHSBuyer()
                {
                    Buyer = createdBuyer,
                    Markup = createdMarkup,
                    ImpersonationConfig = createdImpersonationConfig
                };
            }
            catch (Exception ex)
            {
                LogExt.LogException(_webConfigSettings.AppLogFileKey, Helpers.GetMethodName(), $@"{LoggingNotifications.GetGeneralLogMessagePrefixKey()}", ex.Message, ex.StackTrace, this, true);
            }
            return resp;
        }

        /// <summary>
        /// Public re-usable Update task method for updating a SuperHSBuyer
        /// </summary>
        /// <param name="buyerID"></param>
        /// <param name="superBuyer"></param>
        /// <returns>The newly updated SuperHSBuyer object</returns>
        public async Task<SuperHSBuyer> Update(string buyerID, SuperHSBuyer superBuyer)
        {
            var resp = new SuperHSBuyer();
            try
            {
                // to prevent changing buyerIDs
                superBuyer.Buyer.ID = buyerID;
                var updatedImpersonationConfig = new ImpersonationConfig();

                var updatedBuyer = await _oc.Buyers.SaveAsync<HSBuyer>(buyerID, superBuyer.Buyer);
                var updatedMarkup = await UpdateMarkup(superBuyer.Markup, superBuyer.Buyer.ID);
                if (superBuyer.ImpersonationConfig != null)
                {
                    updatedImpersonationConfig = await SaveImpersonationConfig(superBuyer.ImpersonationConfig, buyerID);
                }
                return new SuperHSBuyer()
                {
                    Buyer = updatedBuyer,
                    Markup = updatedMarkup,
                    ImpersonationConfig = updatedImpersonationConfig
                };
            }
            catch (Exception ex)
            {
                LogExt.LogException(_webConfigSettings.AppLogFileKey, Helpers.GetMethodName(), $@"{LoggingNotifications.GetGeneralLogMessagePrefixKey()}", ex.Message, ex.StackTrace, this, true);
            }
            return resp;
        }

        /// <summary>
        /// Public re-usable task method to get the SuperHSBuyer object by the buyerID
        /// </summary>
        /// <param name="buyerID"></param>
        /// <returns>The SuperHSBuyer response object by the buyerID</returns>
        public async Task<SuperHSBuyer> Get(string buyerID)
        {
            var resp = new SuperHSBuyer();
            try
            {
                var configReq = GetImpersonationByBuyerID(buyerID);
                var buyer = await _oc.Buyers.GetAsync<HSBuyer>(buyerID);
                var config = await configReq;

                // to move into content docs logic
                var markupPercent = buyer.xp?.MarkupPercent ?? 0;
                var markup = new BuyerMarkup()
                {
                    Percent = markupPercent
                };

                return new SuperHSBuyer()
                {
                    Buyer = buyer,
                    Markup = markup,
                    ImpersonationConfig = config
                };
            }
            catch (Exception ex)
            {
                LogExt.LogException(_webConfigSettings.AppLogFileKey, Helpers.GetMethodName(), $@"{LoggingNotifications.GetGeneralLogMessagePrefixKey()}", ex.Message, ex.StackTrace, this, true);
            }
            return resp;
        }

        /// <summary>
        /// Private re-usable GetImpersonationByBuyerID task method to get the ImpersonationConfig object by the buyerID
        /// </summary>
        /// <param name="buyerID"></param>
        /// <returns>The ImpersonationConfig response object by the buyerID</returns>
        private async Task<ImpersonationConfig> GetImpersonationByBuyerID(string buyerID)
        {
            var resp = new ImpersonationConfig();
            try
            {
                var config = await _oc.ImpersonationConfigs.ListAsync(filters: $"BuyerID={buyerID}");
                resp = config?.Items?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                LogExt.LogException(_webConfigSettings.AppLogFileKey, Helpers.GetMethodName(), $@"{LoggingNotifications.GetGeneralLogMessagePrefixKey()}", ex.Message, ex.StackTrace, this, true);
            }
            return resp;
        }

        /// <summary>
        /// Public re-usable CreateBuyerAndRelatedFunctionalResources task method to create the HSBuyer object
        /// </summary>
        /// <param name="buyer"></param>
        /// <param name="accessToken"></param>
        /// <param name="oc"></param>
        /// <returns>The newly created HSBuyer object</returns>
        public async Task<HSBuyer> CreateBuyerAndRelatedFunctionalResources(HSBuyer buyer, string accessToken, IOrderCloudClient oc)
        {
            try
            {
                // if we're seeding then use the passed in oc client
                // to support multiple environments and ease of setup for new orgs
                // else used the configured client
                var token = oc == null ? null : accessToken;
                var ocClient = oc ?? _oc;

                buyer.ID = buyer.ID ?? "{buyerIncrementor}";
                var ocBuyer = await ocClient.Buyers.CreateAsync(buyer, accessToken);
                var ocBuyerID = ocBuyer.ID;
                buyer.ID = ocBuyerID;

                // create base security profile assignment
                await ocClient.SecurityProfiles.SaveAssignmentAsync(new SecurityProfileAssignment
                {
                    BuyerID = ocBuyerID,
                    SecurityProfileID = CustomRole.HSBaseBuyer.ToString()
                }, token);

                // assign message sender
                await ocClient.MessageSenders.SaveAssignmentAsync(new MessageSenderAssignment
                {
                    MessageSenderID = "BuyerEmails",
                    BuyerID = ocBuyerID
                }, token);

                await ocClient.Incrementors.SaveAsync($"{ocBuyerID}-UserIncrementor",
                    new Incrementor { ID = $"{ocBuyerID}-UserIncrementor", LastNumber = 0, LeftPaddingCount = 5, Name = "User Incrementor" }, token);
                await ocClient.Incrementors.SaveAsync($"{ocBuyerID}-LocationIncrementor",
                    new Incrementor { ID = $"{ocBuyerID}-LocationIncrementor", LastNumber = 0, LeftPaddingCount = 4, Name = "Location Incrementor" }, token);

                await ocClient.Catalogs.SaveAssignmentAsync(new CatalogAssignment()
                {
                    BuyerID = ocBuyerID,
                    CatalogID = ocBuyerID,
                    ViewAllCategories = true,
                    ViewAllProducts = false
                }, token);
            }
            catch (Exception ex)
            {
                LogExt.LogException(_webConfigSettings.AppLogFileKey, Helpers.GetMethodName(), $@"{LoggingNotifications.GetGeneralLogMessagePrefixKey()}", ex.Message, ex.StackTrace, this, true);
            }            
            return buyer;
        }

        /// <summary>
        /// Private re-usable CreateMarkup task method to create the BuyerMarkup object
        /// </summary>
        /// <param name="markup"></param>
        /// <param name="buyerID"></param>
        /// <param name="accessToken"></param>
        /// <param name="oc"></param>
        /// <returns>The newly created BuyerMarkup object</returns>
        private async Task<BuyerMarkup> CreateMarkup(BuyerMarkup markup, string buyerID, string accessToken, IOrderCloudClient oc)
        {
            var resp = new BuyerMarkup();
            try
            {
                // if we're seeding then use the passed in oc client
                // to support multiple environments and ease of setup for new orgs
                // else used the configured client
                var token = oc == null ? null : accessToken;
                var ocClient = oc ?? _oc;

                // to move from xp to contentdocs, that logic will go here instead of a patch
                var updatedBuyer = await ocClient.Buyers.PatchAsync(buyerID, new PartialBuyer() { xp = new { MarkupPercent = markup.Percent } }, token);
                resp.Percent = (int)updatedBuyer.xp.MarkupPercent;
            }
            catch (Exception ex)
            {
                LogExt.LogException(_webConfigSettings.AppLogFileKey, Helpers.GetMethodName(), $@"{LoggingNotifications.GetGeneralLogMessagePrefixKey()}", ex.Message, ex.StackTrace, this, true);
            }
            return resp;
        }

        /// <summary>
        /// Private re-usable SaveImpersonationConfig task method to update the ImpersonationConfig object data
        /// </summary>
        /// <param name="impersonation"></param>
        /// <param name="buyerID"></param>
        /// <returns>The updated ImpersonationConfig object</returns>
        private async Task<ImpersonationConfig> SaveImpersonationConfig(ImpersonationConfig impersonation, string buyerID)
        {
            var resp = new ImpersonationConfig();
            try
            {
               resp = await SaveImpersonationConfig(impersonation, buyerID, null, _oc);
            }
            catch (Exception ex)
            {
                LogExt.LogException(_webConfigSettings.AppLogFileKey, Helpers.GetMethodName(), $@"{LoggingNotifications.GetGeneralLogMessagePrefixKey()}", ex.Message, ex.StackTrace, this, true);
            }
            return resp;
        }

        /// <summary>
        /// Private re-usable SaveImpersonationConfig task method to update the ImpersonationConfig object data
        /// </summary>
        /// <param name="impersonation"></param>
        /// <param name="buyerID"></param>
        /// <param name="accessToken"></param>
        /// <param name="oc"></param>
        /// <returns>The updated ImpersonationConfig object</returns>
        private async Task<ImpersonationConfig> SaveImpersonationConfig(ImpersonationConfig impersonation, string buyerID, string accessToken, IOrderCloudClient oc = null)
        {
            var resp = new ImpersonationConfig();
            try
            {
                // if we're seeding then use the passed in oc client
                // to support multiple environments and ease of setup for new orgs
                // else used the configured client
                var token = oc == null ? null : accessToken;
                var ocClient = oc ?? _oc;

                var currentConfig = await GetImpersonationByBuyerID(buyerID);
                if (currentConfig != null && impersonation == null)
                {
                    await ocClient.ImpersonationConfigs.DeleteAsync(currentConfig.ID);
                    return null;
                }
                else if (currentConfig != null)
                {
                    return await ocClient.ImpersonationConfigs.SaveAsync(currentConfig.ID, impersonation, token);
                }
                else
                {
                    impersonation.BuyerID = buyerID;
                    impersonation.SecurityProfileID = Enum.GetName(typeof(CustomRole), CustomRole.HSBaseBuyer);
                    impersonation.ID = $"hs_admin_{buyerID}";
                    return await ocClient.ImpersonationConfigs.CreateAsync(impersonation);
                }
            }
            catch (Exception ex)
            {
                LogExt.LogException(_webConfigSettings.AppLogFileKey, Helpers.GetMethodName(), $@"{LoggingNotifications.GetGeneralLogMessagePrefixKey()}", ex.Message, ex.StackTrace, this, true);
            }
            return resp;
        }

        /// <summary>
        /// Private re-usable UpdateMarkup task method to update the BuyerMarkup object
        /// </summary>
        /// <param name="markup"></param>
        /// <param name="buyerID"></param>
        /// <returns>The newly updated BuyerMarkup object</returns>
        private async Task<BuyerMarkup> UpdateMarkup(BuyerMarkup markup, string buyerID)
        {
            var resp = new BuyerMarkup();
            try
            {
                // to move from xp to contentdocs, that logic will go here instead of a patch
                // currently duplicate of the function above, this might need to be duplicated since there wont be a need to save the contentdocs assignment again
                var updatedBuyer = await _oc.Buyers.PatchAsync(buyerID, new PartialBuyer() { xp = new { MarkupPercent = markup.Percent } });
                resp.Percent = (int)updatedBuyer.xp.MarkupPercent;
            }
            catch (Exception ex)
            {
                LogExt.LogException(_webConfigSettings.AppLogFileKey, Helpers.GetMethodName(), $@"{LoggingNotifications.GetGeneralLogMessagePrefixKey()}", ex.Message, ex.StackTrace, this, true);
            }            
            return resp;
        }
    }
}