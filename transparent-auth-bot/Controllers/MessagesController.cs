using Microsoft.Bot.Connector;
using Microsoft.Graph;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web.Http;

namespace transparent_auth_bot.Controllers
{
    public class UserInfo
    {
        public string DisplayName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public Stream PhotoStream { get; set; }
    }
    [BotAuthentication]
    public class MessagesController : ApiController
    {
        
        public async Task<UserInfo> GetUserInfo(GraphServiceClient graphClient)
        {            
            User me = await graphClient.Me.Request().GetAsync();
            
            return new UserInfo
            {
                Email = me.Mail ?? me.UserPrincipalName,
                DisplayName = me.DisplayName,
                PhotoStream= await graphClient.Users[me.Id].Photo.Content.Request().GetAsync()
            };            
        }
        GraphServiceClient GetAuthenticatedClient(string token)
        {
            GraphServiceClient graphClient = new GraphServiceClient(
                new DelegateAuthenticationProvider(
                    async (requestMessage) =>
                    {
                        string accessToken = token;
                        // Append the access token to the request.
                        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("bearer", accessToken);
                    }));
            return graphClient;
        }
        public async Task<HttpResponseMessage> Post([FromBody]Activity activity)
        {
            switch (activity.GetActivityType())
            {
                case ActivityTypes.Message:
                    var reply = activity.CreateReply();
                    reply.Attachments = new List<Microsoft.Bot.Connector.Attachment>();
                    var cli = new ConnectorClient(new Uri(activity.ServiceUrl));
                    try
                    {

                        StateClient stateClient = activity.GetStateClient();
                        BotState botState = new BotState(stateClient);
                        BotData botData = await botState.GetUserDataAsync(activity.ChannelId, activity.From.Id);
                        string token = botData.GetProperty<string>("GraphAccessToken");
                        
                        if (!string.IsNullOrEmpty(token))
                        {
                            GraphServiceClient gc = GetAuthenticatedClient(token);
                            UserInfo ui = await GetUserInfo(gc);
                            var memoryStream = new MemoryStream();
                            ui.PhotoStream.CopyTo(memoryStream);
                            reply.Attachments.Add(new Microsoft.Bot.Connector.Attachment
                            {
                                Content = memoryStream.ToArray(),
                                ContentType = "image/png"                                
                            });
                            reply.Attachments.Add(new HeroCard()
                            {
                                Title = ui.DisplayName,
                                Text = ui.Email,
                                Images = null,
                                Buttons = null
                            }.ToAttachment());
                           
                            //reply.AttachmentLayout = AttachmentLayoutTypes.Carousel;
                        }
                        else
                        {
                            reply.Text = "No token!";
                        }
                    }
                    catch(Exception ex)
                    {
                        reply.Text = ex.Message;
                    }
                    await cli.Conversations.SendToConversationAsync(reply);
                    break;
                case ActivityTypes.ConversationUpdate:
                    var client = new ConnectorClient(new Uri(activity.ServiceUrl));
                    IConversationUpdateActivity update = activity;

                    if (update.MembersAdded.Any())
                    {
                        
                        var newMembers = update.MembersAdded?.Where(t => t.Id != activity.Recipient.Id);                        
                        foreach (var newMember in newMembers)
                        {
                            var r = activity.CreateReply();
                            r.Text = "Welcome";
                            await client.Conversations.ReplyToActivityAsync(r);
                        }
                    }
                    break;
                case ActivityTypes.ContactRelationUpdate:
                case ActivityTypes.Typing:
                case ActivityTypes.DeleteUserData:
                case ActivityTypes.Ping:
                default:
                    HandleSystemMessage(activity);
                    break;
            }
            var response = Request.CreateResponse(HttpStatusCode.OK);
            return response;

        }
        private Activity HandleSystemMessage(Activity message)
        {
            if (message.Type == ActivityTypes.DeleteUserData)
            {
                // Implement user deletion here
                // If we handle user deletion, return a real message
            }
            else if (message.Type == ActivityTypes.ConversationUpdate)
            {
                // Handle conversation state changes, like members being added and removed
                // Use Activity.MembersAdded and Activity.MembersRemoved and Activity.Action for info
                // Not available in all channels
            }
            else if (message.Type == ActivityTypes.ContactRelationUpdate)
            {
                // Handle add/remove from contact lists
                // Activity.From + Activity.Action represent what happened
            }
            else if (message.Type == ActivityTypes.Typing)
            {
                // Handle knowing tha the user is typing
            }
            else if (message.Type == ActivityTypes.Ping)
            {
            }

            return null;
        }

    }
}
