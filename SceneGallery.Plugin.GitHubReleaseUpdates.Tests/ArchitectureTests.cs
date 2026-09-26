using System.Reflection;
using NetArchTest.Rules;
using SceneGallery.PluginSdk;

namespace SceneGallery.Plugin.GitHubReleaseUpdates.Tests
{
    public sealed class ArchitectureTests
    {
        private static readonly Assembly PluginAssembly = typeof(GitHubReleaseUpdatePlugin).Assembly;

        [Fact]
        public void EveryProviderType_HasAnExplicitArchitecturalHome()
        {
            Type[] expected = [typeof(GitHubReleaseUpdatePlugin), typeof(GitHubReleaseClient),
                typeof(GitHubReleasePolicy), typeof(GitHubRelease), typeof(GitHubReleaseAsset)];
            var providerNamespace = typeof(GitHubReleaseUpdatePlugin).Namespace!;
            var actual = PluginAssembly.GetTypes().Where(type => !type.IsNested &&
                (type.Namespace == providerNamespace ||
                 type.Namespace?.StartsWith(providerNamespace + ".", StringComparison.Ordinal) == true))
                .OrderBy(type => type.FullName).ToArray();
            Assert.Equal(expected.Length, expected.Distinct().Count());
            Assert.Equal(expected.OrderBy(type => type.FullName), actual);
        }

        [Fact]
        public void PublicContractAndCapabilitiesRemainCompatible()
        {
            Assert.Equal("SceneGallery.Plugin.GitHubReleaseUpdates", PluginAssembly.GetName().Name);
            Assert.Equal([typeof(GitHubReleaseUpdatePlugin)], PluginAssembly.GetExportedTypes());
            var plugin = typeof(GitHubReleaseUpdatePlugin);
            Assert.NotNull(plugin.GetConstructor(Type.EmptyTypes));
            Assert.Equal(new[] { typeof(IDisposable), typeof(IPlugin), typeof(IPluginUpdateProvider) }.OrderBy(t => t.FullName),
                plugin.GetInterfaces().OrderBy(t => t.FullName));
            Assert.Equal(new[] { "CanCheckUpdate", "CheckUpdateAsync", "Dispose", "Initialize", "get_Name", "get_Version" },
                plugin.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Select(m => m.Name).OrderBy(n => n, StringComparer.Ordinal));
            // Interface maps pin return/parameter signatures too; no extra public overload is allowed.
            Assert.All(new[] { typeof(IPlugin), typeof(IPluginUpdateProvider), typeof(IDisposable) }, capability =>
                Assert.NotEmpty(plugin.GetInterfaceMap(capability).TargetMethods));
            using var instance = new GitHubReleaseUpdatePlugin();
            Assert.Equal("GitHub Release Updates", instance.Name);
            Assert.Equal("https://github.com/LowTechMaker/GitHubReleaseUpdatePlugin", PluginAssembly
                .GetCustomAttributes<AssemblyMetadataAttribute>().Single(x => x.Key == "PluginUpdateUrl").Value);
        }

        [Fact]
        public void ShippingAssemblyDoesNotReferenceHostOtherPluginsOrTestInfrastructure()
        {
            var references = PluginAssembly.GetReferencedAssemblies();
            Assert.Equal(new Version(1, 0, 0, 0), Assert.Single(references,
                reference => reference.Name == "SceneGallery.PluginSdk").Version);
            Assert.DoesNotContain(references, reference =>
                reference.Name!.StartsWith("KoikatsuSceneGallery", StringComparison.Ordinal)
                || reference.Name.StartsWith("SceneGallery.", StringComparison.Ordinal) && reference.Name != "SceneGallery.PluginSdk"
                || reference.Name.StartsWith("NetArchTest", StringComparison.Ordinal)
                || reference.Name.StartsWith("xunit", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void PolicyAndWireModelsCannotAccessNetworkStorageOrEntryPoint()
        {
            var selected = Types.InAssembly(PluginAssembly).That().HaveNameMatching(
                "^(GitHubReleasePolicy|GitHubRelease|GitHubReleaseAsset)$");
            Assert.Equal(3, selected.GetTypes().Count());
            Assert.True(selected.ShouldNot().HaveDependencyOnAny(
                "System.Net.Http", "System.IO.File", typeof(GitHubReleaseClient).FullName!,
                typeof(GitHubReleaseUpdatePlugin).FullName!, typeof(IPluginHost).FullName!).GetResult().IsSuccessful);
        }

        [Fact]
        public void TransportDoesNotDependOnPolicyOrEntryPoint()
        {
            var selected = Types.InAssembly(PluginAssembly).That().HaveName(nameof(GitHubReleaseClient));
            Assert.Single(selected.GetTypes());
            Assert.True(selected.ShouldNot().HaveDependencyOnAny(
                typeof(GitHubReleaseUpdatePlugin).FullName!, typeof(GitHubReleasePolicy).FullName!,
                typeof(IPluginHost).FullName!).GetResult().IsSuccessful);
        }

        [Theory]
        [InlineData(nameof(Fixtures.Construction))]
        [InlineData(nameof(Fixtures.StaticCall))]
        [InlineData(nameof(Fixtures.AsyncCall))]
        public void DependencyGuardDetectsForbiddenFixtures(string name)
        {
            var selected = Types.InAssembly(typeof(ArchitectureTests).Assembly).That().HaveName(name);
            Assert.Single(selected.GetTypes());
            Assert.False(selected.ShouldNot().HaveDependencyOn(typeof(Fixtures.Dependency).FullName!).GetResult().IsSuccessful);
        }

        [Fact]
        public void DependencyGuardAcceptsPureFixture()
        {
            var selected = Types.InAssembly(typeof(ArchitectureTests).Assembly).That().HaveName(nameof(Fixtures.Pure));
            Assert.Single(selected.GetTypes());
            Assert.True(selected.ShouldNot().HaveDependencyOn(typeof(Fixtures.Dependency).FullName!).GetResult().IsSuccessful);
        }
    }
}

namespace SceneGallery.Plugin.GitHubReleaseUpdates.Tests.Fixtures
{
    internal sealed class Dependency { internal static int Read() => Environment.TickCount; }
    internal sealed class Construction { internal object Run() => new Dependency(); }
    internal sealed class StaticCall { internal int Run() => Dependency.Read(); }
    internal sealed class AsyncCall { internal async Task<int> Run() { await Task.Yield(); return Dependency.Read(); } }
    internal sealed class Pure { internal int Run(int input) => input + 1; }
}
