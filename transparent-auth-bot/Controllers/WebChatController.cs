using Microsoft.Bot.Connector;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;

namespace transparent_auth_bot.Controllers
{
    [Authorize]
    public class WebChatController : ApiController
    {
        private string clientId = ConfigurationManager.AppSettings["ida:ClientId"];
        private string appKey = ConfigurationManager.AppSettings["ida:ClientSecret"];
        private string aadInstance = ConfigurationManager.AppSettings["ida:AADInstance"];
        private string tenantId = ConfigurationManager.AppSettings["ida:TenantId"];
        private string postLogoutRedirectUri = ConfigurationManager.AppSettings["ida:PostLogoutRedirectUri"];
        private string UserAccessToken = null;
        public HttpResponseMessage Get()
        {
            var userId = ClaimsPrincipal.Current.FindFirst(ClaimTypes.NameIdentifier).Value;
            GetTokenViaBootStrap().Wait();
            var botCred = new MicrosoftAppCredentials(
                ConfigurationManager.AppSettings["MicrosoftAppId"],
                ConfigurationManager.AppSettings["MicrosoftAppPassword"]);
            var stateClient = new StateClient(botCred);
            BotState botState = new BotState(stateClient);
            BotData botData = new BotData(eTag: "*");
            botData.SetProperty<string>("GraphAccessToken", UserAccessToken);
            stateClient.BotState.SetUserDataAsync("webchat", userId, botData).Wait();
            string WebChatString =
                new WebClient().DownloadString("https://webchat.botframework.com/embed/transparentauth?s=PJ6liqNOs3Q.cwA.Xv0.D-h-4BEcvEH_Om2DcLcJBu1B9GNAvKXh5uJUsve7A5k&userid=" +
                HttpUtility.UrlEncode(userId) + "&username=" + HttpUtility.UrlEncode(ClaimsPrincipal.Current.Identity.Name));            
            WebChatString = WebChatString.Replace("/css/botchat.css", "https://webchat.botframework.com/css/botchat.css");
            WebChatString = WebChatString.Replace("/scripts/botchat.js", "https://webchat.botframework.com/scripts/botchat.js");
            var response = new HttpResponseMessage();
            response.Content = new StringContent(WebChatString);
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html");
            return response;
        }
        private Task GetTokenViaBootStrap()
        {
            return Task.Run(async () =>
            {
                var bc = ClaimsPrincipal.Current.Identities.First().BootstrapContext
                    as System.IdentityModel.Tokens.BootstrapContext;

                string userName = ClaimsPrincipal.Current.FindFirst(ClaimTypes.Upn) != null ? ClaimsPrincipal.Current.FindFirst(ClaimTypes.Upn).Value : ClaimsPrincipal.Current.FindFirst(ClaimTypes.Email).Value;
                string userAccessToken = bc.Token;
                UserAssertion userAssertion = new UserAssertion(bc.Token, "urn:ietf:params:oauth:grant-type:jwt-bearer", userName);
                string userObjectID = ClaimsPrincipal.Current.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier").Value;
                string signedInUserID = ClaimsPrincipal.Current.FindFirst(ClaimTypes.NameIdentifier).Value;
                AuthenticationContext authContext = new AuthenticationContext(aadInstance + tenantId, new ADALTokenCache(signedInUserID));
                ClientCredential cred = new ClientCredential(clientId,appKey);
                AuthenticationResult res = await authContext.AcquireTokenAsync("https://graph.microsoft.com", cred,
                    userAssertion);
                UserAccessToken = res.AccessToken;
            });

        }
    }
}
