﻿// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Squidex.ClientLibrary
{
    public abstract class SquidexClientBase
    {
        protected Uri ServiceUrl { get; }

        protected string ApplicationName { get; }

        protected IAuthenticator Authenticator { get; }

        protected SquidexClientBase(Uri serviceUrl, string applicationName, string schemaName, IAuthenticator authenticator)
        {
            Guard.NotNull(serviceUrl, nameof(serviceUrl));
            Guard.NotNull(authenticator, nameof(authenticator));
            Guard.NotNullOrEmpty(applicationName, nameof(applicationName));

            ApplicationName = applicationName;
            Authenticator = authenticator;
            ServiceUrl = serviceUrl;
        }

        protected async Task<HttpResponseMessage> RequestAsync(HttpMethod method, string path, HttpContent content = null, QueryContext context = null)
        {
            var uri = new Uri(ServiceUrl, path);

            var requestToken = await Authenticator.GetBearerTokenAsync();
            var request = BuildRequest(method, content, uri, requestToken);

            context?.AddToHeaders(request.Headers);

            var response = await SquidexHttpClient.Instance.SendAsync(request);

            await EnsureResponseIsValidAsync(response, requestToken);

            return response;
        }

        protected static HttpRequestMessage BuildRequest(HttpMethod method, HttpContent content, Uri uri, string requestToken)
        {
            var request = new HttpRequestMessage(method, uri);

            if (content != null)
            {
                request.Content = content;
            }

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", requestToken);

            return request;
        }

        protected async Task EnsureResponseIsValidAsync(HttpResponseMessage response, string token)
        {
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    await Authenticator.RemoveTokenAsync(token);
                }

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new SquidexException("The app, schema or entity does not exist.");
                }

                if ((int)response.StatusCode == 429)
                {
                    throw new SquidexException("Too many requests, please upgrade your subscription.");
                }

                var message = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrWhiteSpace(message))
                {
                    message = "Squidex API failed with internal error.";
                }
                else
                {
                    message = $"Squidex Request failed: {message}";
                }

                throw new SquidexException(message);
            }
        }
    }
}
