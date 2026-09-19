using Unity.Netcode.Components;
using UnityEngine;

namespace OfficeImposter
{
    // Owner drives its own transform; the server relays it. Without this the default
    // NetworkTransform is server-authoritative and the local player rubber-bands.
    [DisallowMultipleComponent]
    public class ClientNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative() => false;
    }
}
