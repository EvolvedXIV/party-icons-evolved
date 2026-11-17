using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace PartyIconsEvolved.Nameplates;

/// <summary>
/// Creates and destroys the native nodes we add to each nameplate: an image node for the job icon and a text node
/// for a free-floating role indicator. They live inside the game's own nameplate component, so they are drawn,
/// depth-sorted and hidden exactly like the rest of the nameplate.
/// </summary>
internal static unsafe class NativeNodes
{
    /// <summary>Width and height of the job icon node.</summary>
    public const ushort IconSize = 32;

    // Size of the indicator text node, in its own units. The text is centred in it, so this only has to be large
    // enough to never clip.
    public const ushort TextWidth = 240;
    public const ushort TextHeight = 64;

    private const NodeFlags BaseFlags = NodeFlags.AnchorTop | NodeFlags.AnchorLeft | NodeFlags.Enabled |
                                        NodeFlags.EmitsEvents | NodeFlags.UseDepthBasedPriority;

    /// <summary>Allocates an image node with a single 32x32 part. Returns null if the game is out of UI memory.</summary>
    public static AtkImageNode* CreateImage(uint nodeId)
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
        part->Width = IconSize;
        part->Height = IconSize;

        partsList->Id = 0;
        partsList->PartCount = 1;
        partsList->Parts = part;

        node->Type = NodeType.Image;
        node->NodeId = nodeId;
        node->NodeFlags = BaseFlags;
        node->DrawFlags = 0;
        node->PartsList = partsList;
        node->PartId = 0;
        node->WrapMode = 1;
        node->Flags = ImageNodeFlags.AutoFit;
        node->SetWidth(IconSize);
        node->SetHeight(IconSize);

        return node;
    }

    /// <summary>
    /// Allocates a text node that renders like <paramref name="template"/> (the nameplate's own name text): same
    /// font, size and outline. Returns null if the game is out of UI memory.
    /// </summary>
    public static AtkTextNode* CreateText(uint nodeId, AtkTextNode* template)
    {
        var node = IMemorySpace.GetUISpace()->Create<AtkTextNode>();
        if (node == null) {
            return null;
        }

        node->Type = NodeType.Text;
        node->NodeId = nodeId;
        node->NodeFlags = BaseFlags;
        node->DrawFlags = 0;
        node->SetWidth(TextWidth);
        node->SetHeight(TextHeight);

        node->FontSize = template->FontSize;
        node->FontType = template->FontType;
        node->AlignmentType = AlignmentType.Center;
        node->LineSpacing = template->LineSpacing;
        node->CharSpacing = template->CharSpacing;

        // Keep the look (outline, weight) but none of the layout behaviour of the name text.
        const TextFlags look = TextFlags.Bold | TextFlags.Italic | TextFlags.Edge | TextFlags.Glare | TextFlags.Emboss;
        node->TextFlags = (template->TextFlags & look) | TextFlags.Edge;

        // Defaults for anything the text itself does not colour.
        node->TextColor.R = 255;
        node->TextColor.G = 255;
        node->TextColor.B = 255;
        node->TextColor.A = 255;
        node->EdgeColor.R = 0;
        node->EdgeColor.G = 0;
        node->EdgeColor.B = 0;
        node->EdgeColor.A = 255;

        return node;
    }

    /// <summary>Appends the node as the last sibling among the children of <paramref name="parent"/>.</summary>
    public static bool Link(AtkResNode* node, AtkResNode* parent, AtkUldManager* uldManager)
    {
        var last = parent->ChildNode;
        if (last == null) {
            return false;
        }

        while (last->PrevSiblingNode != null) {
            last = last->PrevSiblingNode;
        }

        last->PrevSiblingNode = node;
        node->NextSiblingNode = last;
        node->PrevSiblingNode = null;
        node->ParentNode = last->ParentNode;

        if (node->ParentNode != null && node->ParentNode->NextSiblingNode == node->NextSiblingNode) {
            node->ParentNode->NextSiblingNode = node;
        }

        uldManager->UpdateDrawNodeList();
        return true;
    }

    /// <summary>Takes the node out of the nameplate. It still has to be freed.</summary>
    public static void Unlink(AtkResNode* node, AtkUldManager* uldManager)
    {
        if (node->ParentNode != null && node->ParentNode->NextSiblingNode == node) {
            node->ParentNode->NextSiblingNode = node->NextSiblingNode;
        }

        if (node->PrevSiblingNode != null) {
            node->PrevSiblingNode->NextSiblingNode = node->NextSiblingNode;
        }

        if (node->NextSiblingNode != null) {
            node->NextSiblingNode->PrevSiblingNode = node->PrevSiblingNode;
        }

        uldManager->UpdateDrawNodeList();
    }

    /// <summary>Frees an unlinked image node and everything <see cref="CreateImage"/> allocated for it.</summary>
    public static void FreeImage(AtkImageNode* node)
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

    /// <summary>Frees an unlinked text node.</summary>
    public static void FreeText(AtkTextNode* node)
    {
        node->Destroy(false);
        IMemorySpace.Free(node, (ulong)sizeof(AtkTextNode));
    }

    /// <summary>Finds a node by id and type in a component's node list.</summary>
    public static AtkResNode* Find(AtkUldManager* uldManager, uint nodeId, NodeType type)
    {
        if (uldManager->NodeList == null) {
            return null;
        }

        for (var i = 0; i < uldManager->NodeListCount; i++) {
            var candidate = uldManager->NodeList[i];
            if (candidate != null && candidate->NodeId == nodeId && candidate->Type == type) {
                return candidate;
            }
        }

        return null;
    }
}
