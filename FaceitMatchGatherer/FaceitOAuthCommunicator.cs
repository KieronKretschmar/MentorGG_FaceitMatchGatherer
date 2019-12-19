using Entities.Models;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using static Entities.Enumerals;

namespace FaceitMatchGatherer
{
    public interface IFaceitOAuthCommunicator
    {
        Task<User> CreateUser(long steamId, string code);
    }

    /// <summary>
    /// Implements Faceit's OAuth2 Authorization Code Flow
    /// For more information see 
    /// https://developers-support.faceit.com/hc/en-us/articles/115001594504-FACEIT-Connect-Documentation
    /// 
    /// Requires environment variables: ["FACEIT_OAUTH_CLIENT_ID", "FACEIT_OAUTH_CLIENT_SECRET", "FACEIT_OAUTH_TOKEN_ENDPOINT"]
    /// </summary>
    public class FaceitOAuthCommunicator : IFaceitOAuthCommunicator
    {
        private readonly string clientId;
        private readonly string clientSecret;
        private readonly string tokenEndpoint;

        public FaceitOAuthCommunicator(IConfiguration configuration)
        {
            clientId = configuration.GetValue<string>("FACEIT_OAUTH_CLIENT_ID");
            clientSecret = configuration.GetValue<string>("FACEIT_OAUTH_CLIENT_SECRET");
            tokenEndpoint = configuration.GetValue<string>("FACEIT_OAUTH_TOKEN_ENDPOINT");
        }

        public async Task<User> CreateUser(long steamId, string code)
        {
            try
            {
                string postData = "code=" + code + "&grant_type=authorization_code";
                var responseJson = await RequestFaceIt(postData);

                // The following tokens are required when making further requests on behalf of the user
                // The access token you can use to call the Faceit APIs
                var accessToken = responseJson["access_token"].ToString();
                // The refresh token you need to use to get a new access token when your current one expires
                var refreshToken = responseJson["refresh_token"].ToString();
                //The remaining lifetime of your access token in seconds
                var expiresIn = long.Parse(responseJson["expires_in"].ToString());

                // handle id_token containing payload-data (faceitname, first name, last name, subscription type...) 
                var handler = new JwtSecurityTokenHandler();
                var idToken = handler.ReadToken(responseJson["id_token"].ToString()) as JwtSecurityToken;
                var faceitId = idToken.Claims.First(x => x.Type == "guid").Value;
                var faceitName = idToken.Claims.First(x => x.Type == "nickname").Value;
                var membershipString = idToken.Claims.First(x => x.Type == "membership").Value; // Free/Unlimited/Premium
                var membership = Enum.TryParse(membershipString.ToUpperInvariant(), out FaceitMembership result) ? result : FaceitMembership.Unknown;

                var user = new User
                {
                    SteamId = steamId,
                    FaceitId = faceitId,
                    FaceitMembership = membership,
                    FaceitName = faceitName,
                    RefreshToken = refreshToken,
                    Token = accessToken,
                    TokenExpires = DateTime.Now.AddSeconds(expiresIn),
                    LastChecked = DateTime.MinValue,
                };

                return user;
            }
            catch (Exception e)
            {
                throw new FaceitFailedUserCreationException("", e);
            }


        }


        /// <summary>
        /// Performs POST Request to FACEIT token_endpoint.
        /// For more details see http://assets1.faceit.com/third_party/docs/Faceit_Connect.pdf
        /// </summary>
        /// <param name="content"></param>
        /// <returns>JObject containing the response</returns>
        private async Task<JObject> RequestFaceIt(string content)
        {
            // Build request
            var request = (HttpWebRequest)WebRequest.Create(tokenEndpoint);
            request.Method = "POST";

            // Request header
            CredentialCache credentialCache = new CredentialCache();
            credentialCache.Add(new Uri(tokenEndpoint), "Basic", new NetworkCredential(clientId, clientSecret));
            request.Credentials = credentialCache;


            // Request body
            request.ContentType = "application/x-www-form-urlencoded";
            byte[] contentBytes = Encoding.ASCII.GetBytes(content);
            request.ContentLength = contentBytes.Length;
            using (Stream requestStream = request.GetRequestStream())
            {
                requestStream.Write(contentBytes, 0, contentBytes.Length);
            }

            // Read response
            JObject responsejson;
            using (var response = (HttpWebResponse)await request.GetResponseAsync())
            {
                using (var reader = new StreamReader(response.GetResponseStream(), Encoding.ASCII))
                {
                    responsejson = JObject.Parse(reader.ReadToEnd());
                }
            }

            return responsejson;
        }

        public class FaceitFailedUserCreationException : Exception
        {
            public FaceitFailedUserCreationException(string message) : base(message)
            {
            }

            public FaceitFailedUserCreationException(string message, Exception innerException) : base(message, innerException)
            {
            }
        }
    }
}
