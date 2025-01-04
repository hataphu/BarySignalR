using Microsoft.Extensions.DependencyInjection;

namespace BarySignalR.Backplane.GrainAdaptors
{
    /// <summary>
    /// Provides the grain factory to the BarySignalR backplane.
    /// See <see cref="GrainFactoryProvider"/> for a simple implementation.
    /// </summary>
    public interface IGrainFactoryProvider
    {
        IGrainFactory GetGrainFactory();
    }

    public class GrainFactoryProvider : IGrainFactoryProvider
    {
        private readonly IServiceProvider serviceProvider;

        public GrainFactoryProvider(IServiceProvider serviceProvider)
        {
            this.serviceProvider =
                serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public IGrainFactory GetGrainFactory() =>
            serviceProvider.GetRequiredService<IClusterClient>();
    }
}
