using Microsoft.Extensions.DependencyInjection;
using BarySignalR.Backplane.GrainAdaptors;
using BarySignalR.Backplane.GrainImplementations;
using BarySignalR.Core;
using BarySignalR.Core.Provider;
namespace BarySignalR.Silo
{
    public static class Extensions
    {
        public const string GROUP_STORAGE_PROVIDER = Constants.GROUP_STORAGE_PROVIDER;
        public const string USER_STORAGE_PROVIDER = Constants.USER_STORAGE_PROVIDER;

        /// <summary>
        /// This will store messages for each SignalR message stream, allowing clients to resubscribe without missing any messages
        /// This is a best effort resubscribe, and can be configured via <see cref="BarySignalRSiloConfig"/>
        /// </summary>
        public const string MESSAGE_STORAGE_PROVIDER = Constants.MESSAGE_STORAGE_PROVIDER;

        /// <summary>
        /// Adds the BarySignalR grains to the builder, and also automatically registers memory grain storage for group and user lists.
        /// This is useful for local development, however it is recommended that you add a persistent storage for:
        /// <see cref="GROUP_STORAGE_PROVIDER"/>, and <see cref="USER_STORAGE_PROVIDER"/>, and <see cref="MESSAGE_STORAGE_PROVIDER"/>
        /// Then you may use <see cref="AddBarySignalR<T>(T builder)"/> to add BarySignalR using the storage providers of your choice
        /// </summary>
        /// <param name="builder">The builder to configure</param>
        /// <returns>The silo builder, configured with memory storage and grains for the BarySignalR backplane</returns>
        public static ISiloBuilder AddBarySignalRWithMemoryGrainStorage(
            this ISiloBuilder builder,
            Action<BarySignalRSiloConfig>? configure = null
        )
        {
            try
            {
                builder.AddMemoryGrainStorage(Constants.GROUP_STORAGE_PROVIDER);
            }
            catch
            { /* Do nothing, already added  */
            }
            try
            {
                builder.AddMemoryGrainStorage(Constants.USER_STORAGE_PROVIDER);
            }
            catch
            { /* Do nothing, already added  */
            }
            try
            {
                builder.AddMemoryGrainStorage(Constants.MESSAGE_STORAGE_PROVIDER);
            }
            catch
            { /* Do nothing, already added  */
            }

            return builder.AddBarySignalR(configure);
        }

        /// <summary>
        /// Adds the BarySignalR grains to the builder. This method is recommended for production use.
        /// You must configure storage providers for:
        /// <see cref="GROUP_STORAGE_PROVIDER"/>, and <see cref="USER_STORAGE_PROVIDER"/>, and <see cref="MESSAGE_STORAGE_PROVIDER"/>
        /// Alternatively, for local development, use: <see cref="AddBarySignalRWithMemoryGrainStorage<T>(T builder)"/>
        /// </summary>
        /// <param name="builder">The builder to configure</param>
        /// <returns>The silo builder, configured with grains for the BarySignalR backplane</returns>
        public static ISiloBuilder AddBarySignalR(
            this ISiloBuilder builder,
            Action<BarySignalRSiloConfig>? configure = null
        )
        {
            builder.ConfigureServices(
                (services) =>
                {
                    var conf = new BarySignalRSiloConfig();
                    configure?.Invoke(conf);
                    services.Add(new ServiceDescriptor(typeof(BarySignalRSiloConfig), conf));

                    services.AddSingleton<IGrainFactoryProvider, GrainFactoryProvider>();
                    builder.Services.AddSingleton<
                        IMessageArgsSerializer,
                        OrleansMessageArgsSerializer
                    >();
                    services.AddSingleton<IActorProviderFactory, GrainActorProviderFactory>();
                    services.AddSingleton<IHubContextProvider, HubContextProvider>();
                }
            );
            return builder;
        }
    }
}
