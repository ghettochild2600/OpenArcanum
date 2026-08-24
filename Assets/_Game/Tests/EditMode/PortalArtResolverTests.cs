using Arcanum.Formats.Art;
using Arcanum.Formats.Text;
using NUnit.Framework;

namespace Arcanum.Formats.Tests
{
    public sealed class PortalArtResolverTests
    {
        [Test]
        public void WindowSlotDerivesAuthoritativePortalIdentityFromWall()
        {
            var resolver = PortalArtResolver.FromMes(
                MesReader.Read("{1125}{in5f5au0.art 0}"));

            // Real Dernholm sector pair: the stored generic portal is 0x33E03811, while
            // wall in5w5au0.art carries the authoritative window slot and rotation.
            uint? derived = resolver.DeriveFromWall(0x10047811u, "art/wall/in5w5au0.art");

            Assert.That(derived, Is.EqualTo(0x33E03C00u));
            Assert.That(resolver.Resolve(derived.Value), Is.EqualTo("art/portal/in5f5au0.art"));
        }

        [Test]
        public void OrdinaryWallSlotDoesNotInventPortalArt()
        {
            var resolver = PortalArtResolver.FromMes(MesReader.Read("{1125}{in5f5au0.art 0}"));

            Assert.That(
                resolver.DeriveFromWall(0x10000000u, "art/wall/in5bseu0.art"),
                Is.Null);
        }
    }
}
