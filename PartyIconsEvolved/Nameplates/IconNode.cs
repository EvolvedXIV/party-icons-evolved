using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace PartyIconsEvolved.Nameplates;

/// <summary>
/// Creates and destroys the native image node that shows the job icon. The node lives inside the game's own
/// nameplate component, so it is drawn, depth-sorted and hidden exactly like the rest of the nameplate.
/// </summary>
internal static unsafe class IconNode
{
    public const ushort Size = 32;

    /// <summary>Allocates an image node with a single 32x32 part. Returns null if the game is out of UI memory.</summary>
    public static AtkImageNode* Create(uint nodeId)
    {
        var space = IMemorySpace.GetUISpace();

        var node = space->Create<AtkImageNode>();
        var partsList = (AtkUldPartsList*)space->Malloc((ulong)sizeof(AtkUldPartsList), 8);
        var part = (AtkUldPart*)space->Malloc((ulong)sizeof(AtkUldPart), 8);
        var asset = (AtkUldAsset*)space->Malloc((ulong)sizeof(AtkUldAsset), 8);

        if (node == null || partsList == null || part == null || asset == null) {
            if (asset != null) IMemorySpace.Free(asset, (ulong)sizeof(AtkUldAsset));
            if (part != null) IMemorySpace.Free(part, (ulong)sizeof(AtkUldPart));
            if (partsList != null) IMemorySpace.Free(partsList, (ulong)sizeof(AtkUldPartsList));
            if (node != null) {
                node->Destroy(false);
                IMemorySpace.Free(node, (ulong)sizeof(AtkImageNode));
            }

            return null;
        }

        asset->Id = 0;
        asset->AtkTexture.Ctor();

        part->UldAsset = asset;
        part->U = 0;
        part->V = 0;
        part->Width = Size;
        part->Height = Size;

        partsList->Id = 0;
        partsList->PartCount = 1;
        partsList->Parts = part;

        node->Type = NodeType.Image;
        node->NodeId = nodeId;
        node->NodeFlags = NodeFlags.AnchorTop | NodeFlags.AnchorLeft | NodeFlags.Enabled | NodeFlags.EmitsEvents |
                          NodeFlags.UseDepthBasedPriority;
        node->DrawFlags = 0;
        node->PartsList = partsList;
        node->PartId = 0;
        node->WrapMode = 1;
        node->Flags = ImageNodeFlags.AutoFit;
        node->SetWidth(Size);
        node->SetHeight(Size);

        return node;
    }

    /// <summary>Appends the node as the last sibling among the children of <paramref name="parent"/>.</summary>
    public static bool Link(AtkImageNode* node, AtkResNode* parent, AtkUldManager* uldManager)
    {
        var last = parent->ChildNode;
        if (last == null) {
            return false;
        }

        while (last->PrevSiblingNode != null) {
            last = last->PrevSiblingNode;
        }

        var res = (AtkResNode*)node;
        last->PrevSiblingNode = res;
        res->NextSiblingNode = last;
        res->PrevSiblingNode = null;
        res->ParentNode = last->ParentNode;

        if (res->ParentNode != null && res->ParentNode->NextSiblingNode == res->NextSiblingNode) {
            res->ParentNode->NextSiblingNode = res;
        }

        uldManager->UpdateDrawNodeList();
        return true;
    }

    /// <summary>Removes the node from the nameplate and frees everything <see cref="Create"/> allocated.</summary>
    public static void Destroy(AtkImageNode* node, AtkUldManager* uldManager)
    {
        var res = (AtkResNode*)node;

        if (res->ParentNode != null && res->ParentNode->NextSiblingNode == res) {
            res->ParentNode->NextSiblingNode = res->NextSiblingNode;
        }

        if (res->PrevSiblingNode != null) {
            res->PrevSiblingNode->NextSiblingNode = res->NextSiblingNode;
        }

        if (res->NextSiblingNode != null) {
            res->NextSiblingNode->PrevSiblingNode = res->PrevSiblingNode;
        }

        uldManager->UpdateDrawNodeList();
        Free(node);
    }

    /// <summary>Frees a node that is not (or no longer) linked into a nameplate.</summary>
    public static void Free(AtkImageNode* node)
    {
        var partsList = node->PartsList;
        if (partsList != null) {
            for (var i = 0u; i < partsList->PartCount; i++) {
                var asset = partsList->Parts[i].UldAsset;
                if (asset != null) {
                    asset->AtkTexture.ReleaseTexture();
                    IMemorySpace.Free(asset, (ulong)sizeof(AtkUldAsset));
                }
            }

            IMemorySpace.Free(partsList->Parts, (ulong)sizeof(AtkUldPart) * partsList->PartCount);
            IMemorySpace.Free(partsList, (ulong)sizeof(AtkUldPartsList));
        }

        node->Destroy(false);
        IMemorySpace.Free(node, (ulong)sizeof(AtkImageNode));
    }

    /// <summary>Finds an image node by id in a component's node list.</summary>
    public static AtkImageNode* Find(AtkUldManager* uldManager, uint nodeId)
    {
        if (uldManager->NodeList == null) {
            return null;
        }

        for (var i = 0; i < uldManager->NodeListCount; i++) {
            var candidate = uldManager->NodeList[i];
            if (candidate != null && candidate->NodeId == nodeId && candidate->Type == NodeType.Image) {
                return (AtkImageNode*)candidate;
            }
        }

        return null;
    }
}
