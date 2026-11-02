using Unity.Netcode.Components;

namespace SkyBout.Online
{
    public sealed class OwnerNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative() => false;
    }
}
