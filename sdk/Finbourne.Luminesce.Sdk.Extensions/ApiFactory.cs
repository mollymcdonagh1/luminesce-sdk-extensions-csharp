using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Finbourne.Luminesce.Sdk.Client;

namespace Finbourne.Luminesce.Sdk.Extensions
{
    /// <summary>
    /// Factory to provide instances of the autogenerated Api
    /// </summary>
    public interface IApiFactory
    {
        /// <summary>
        /// Return the specific autogenerated Api
        /// </summary>
        TApi Api<TApi>() where TApi : class, IApiAccessor;
    }

    /// <inheritdoc />
    public class ApiFactory : IApiFactory
    {
        private static readonly IEnumerable<Type> ApiTypes = Assembly.GetAssembly(typeof(ApiClient))
            .GetTypes()
            .Where(t => typeof(IApiAccessor).IsAssignableFrom(t) && t.IsClass);

        private readonly IReadOnlyDictionary<Type, IApiAccessor> _apis;

        /// <summary>
        /// Create a new factory using the specified configuration
        /// </summary>
        /// <param name="apiConfiguration">Configuration for the ClientCredentialsFlowTokenProvider, usually sourced from a "secrets.json" file</param>
        public ApiFactory(ApiConfiguration apiConfiguration)
        {
            if (apiConfiguration == null) throw new ArgumentNullException(nameof(apiConfiguration));

            // Validate Uris
            if (!Uri.TryCreate(apiConfiguration.TokenUrl, UriKind.Absolute, out var _))
            {
                throw new UriFormatException($"Invalid Token Uri: {apiConfiguration.TokenUrl}");
            }

            if (!Uri.TryCreate(apiConfiguration.ApiUrl, UriKind.Absolute, out var _))
            {
                throw new UriFormatException($"Invalid Uri: {apiConfiguration.ApiUrl}");
            }

            // Create configuration
            var tokenProvider = new ClientCredentialsFlowTokenProvider(apiConfiguration);
            var configuration = new TokenProviderConfiguration(tokenProvider)
            {
                BasePath = apiConfiguration.ApiUrl,
            };
            
            configuration.DefaultHeaders.Add("X-LUSID-Application", apiConfiguration.ApplicationName);

            _apis = Init(configuration);
        }

        /// <summary>
        /// Create a new factory using the specified configuration
        /// </summary>
        /// <param name="configuration">A set of configuration settings</param>
        public ApiFactory(Configuration configuration)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            _apis = Init(configuration);
        }

        /// <inheritdoc />
        public TApi Api<TApi>() where TApi : class, IApiAccessor
        {
            _apis.TryGetValue(typeof(TApi), out var api);

            if (api == null)
            {
                throw new InvalidOperationException($"Unable to find api: {typeof(TApi)}");
            }

            return api as TApi;
        }

        private static Dictionary<Type, IApiAccessor> Init(Configuration configuration)
        {
            var dict = new Dictionary<Type, IApiAccessor>();
            foreach (Type api in ApiTypes)
            {
                if (!(Activator.CreateInstance(api, configuration) is IApiAccessor impl))
                {
                    throw new Exception($"Unable to create type {api}");
                }

                var @interface = api.GetInterfaces()
                    .First(i => typeof(IApiAccessor).IsAssignableFrom(i));

                dict[api] = impl;
                dict[@interface] = impl;
            }

            return dict;
        }
    }
}