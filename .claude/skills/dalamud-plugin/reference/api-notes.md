# Dalamud API notes (API level 15, Dalamud 15.0.4)

Every signature here was compiled against the assemblies in `~/.xlcore/dalamud/Hooks/dev`. "Seen in game"
means the user confirmed the feature built on it works; otherwise it compiled but was not observed running.
Re-check anything you rely on with `tools/api.sh` / `tools/xmldoc.py` – APIs move between levels.

## Nameplates (`INamePlateGui`) – seen in game (v1.0.0)

```csharp
Service.NamePlateGui.OnNamePlateUpdate     += (INamePlateUpdateContext ctx, IReadOnlyList<INamePlateUpdateHandler> handlers) => { };
Service.NamePlateGui.OnPostNamePlateUpdate += ...;   // same delegate; runs after the game laid plates out
Service.NamePlateGui.RequestRedraw();                // after settings/role changes; cheap enough on demand, not every frame
```

- `OnNamePlateUpdate` only lists plates with "important updates"; `OnDataUpdate`/`OnPostDataUpdate` list all
  plates every frame (unused here).
- Per handler: `NamePlateKind` (`NamePlateKind.PlayerCharacter` …), `NamePlateIndex` (index into the addon's
  `NamePlateObjectArray` – use this, not `ArrayIndex`), `PlayerCharacter` (`IPlayerCharacter?`), `GameObject`.
- Text: `Name` (SeString get/set), `NameParts.Text` (SeString) and `NameParts.TextWrap`
  (`(SeString, SeString)?` prefix/suffix) – the cooperative builder; prefer it over overwriting `Name`.
  Same for `TitleParts`, `FreeCompanyTagParts`. Raw access: `GetFieldAsSpan(NamePlateStringField.X)`,
  `SetField(field, SeString|string|bytes)`.
- `RemoveName()/RemoveFreeCompanyTag()/RemoveStatusPrefix()/RemoveTitle()` write a special empty pointer that
  other setters then **refuse to overwrite** – use them only when you really want the field gone for everyone.
- `DisplayTitle` (bool), `IsPrefixTitle` (title above vs below the name).
- `NameIconId` (int): the small icon left of the name. `-1` disables. **Trick (from xivPartyIcons):** set it
  to the blank icon `61696` so the game always lays out its `NameIcon` image node; that gives a reliable anchor
  next to the name for your own nodes.
- Name shortening must respect the game's Display Name Settings: modify the `Name` the handler already holds
  (it is already abbreviated by the game), keeping non-text payloads so colour pushes/pops stay balanced
  (`NameplateText.Shorten`). User's setting can be read with
  `IGameConfig.TryGet(UiConfigOption.NamePlateNameTypeParty, out uint v)` (also `...Self`, `...Alliance`,
  `...Other`, `...Friend`). Value mapping 0 Full / 1 Surname abbr. / 2 Forename abbr. / 3 Initials is
  **(unverified)**.

### Native nodes inside nameplates (FFXIVClientStructs)

Lifecycle that works (copied from xivPartyIcons, seen in game for the image node):

1. `IAddonLifecycle` `PreDraw` on `"NamePlate"`: skip the first call (addon may still be setting up), then
   create nodes; if any component's `UldManager.LoadedState != AtkLoadState.Loaded`, return and retry next frame.
2. `PreFinalize`: reset and destroy nodes, go back to waiting (happens on logout/relog).
3. Dispose: reset plates, destroy nodes via `GameGui.GetAddonByName("NamePlate").Address`, `RequestRedraw()`.

Layout (`AddonNamePlate`, `AddonNamePlate.NamePlateObject`):
- `addon->NamePlateObjectArray[i]` for `i < AddonNamePlate.NumNamePlateObjects` (static).
- Object fields: `RootComponentNode`, `NameContainer` (AtkResNode*, parent of the text), `NameText`
  (AtkTextNode*), `NameIcon` (AtkImageNode*), `NameplateCollision`, `TextW`, `NamePlateKind` (`UIObjectKind`).
- Component's ULD manager: `NameContainer->ParentNode->GetAsAtkComponentNode()->Component->UldManager`.
- Name container geometry in its own coordinates: centre X 144, bottom Y 107 (constants from xivPartyIcons).

Creating nodes (see `PartyIconsEvolved/Nameplates/NativeNodes.cs`):
- `IMemorySpace.GetUISpace()->Create<AtkImageNode>()` / `Create<AtkTextNode>()`; parts list/part/asset via
  `->Malloc((ulong)sizeof(T), 8)`; asset `AtkTexture.Ctor()`; free with `IMemorySpace.Free(ptr, size)` after
  `node->Destroy(false)`.
- Link as last child of `NameContainer` by sibling-pointer surgery, then `uldManager->UpdateDrawNodeList()`.
  Give nodes unique `NodeId`s so leftovers can be found again (`uldManager->NodeList[i]->NodeId`).
- `imageNode->LoadIconTexture(iconId, 0)`; `SetScale`, `SetPositionFloat`, `OriginX/OriginY`,
  `ToggleVisibility(bool)`.
- Every frame (PreDraw) copy `Visible | UseDepthBasedPriority` from `NameText->NodeFlags` onto your nodes, or
  they show through walls / when the plate is hidden.
- If you scale `NameContainer`, also scale `NameplateCollision` (origin = width/2, height) and reset both
  when the plate stops being yours.
- Text node from scratch **(not yet seen in game)**: copy `FontSize`, `FontType`, `LineSpacing`,
  `CharSpacing` and look flags (`TextFlags.Edge|Bold|Glare...`) from `NameText`; `AlignmentType.Center`;
  `SetText(byte*)` needs **null-terminated** encoded SeString bytes; `NameText->GetTextDrawSize(&w, &h, text)`
  measures text (scale by `NameText->ScaleX`).
- Game job icon ids (API 15 era): framed `62100 + jobId`, embossed `62000 + jobId`; other sets need tables
  (`PartyIconsEvolved/Game/JobData.cs`, from xivPartyIcons' `IconRegistrar`).

## Context menu (`IContextMenu`) – part of v1.0.0 (reported working)

```csharp
Service.ContextMenu.OnMenuOpened += args => {
    if (args is not { MenuType: ContextMenuType.Default, Target: MenuTargetDefault target }) return;
    // target.TargetCharacter?.Name / .HomeWorld.RowId  (party list, chat, etc.)
    // target.TargetObject as IPlayerCharacter          (in-world right click)
    // target.TargetName / TargetContentId / TargetHomeWorld.RowId as fallback
    // args.AddonName == "_PartyList" when opened from the party list
    args.AddMenuItem(new MenuItem {
        Name = "Role Indicator", PrefixChar = 'R', PrefixColor = 37,  // UIColor row
        IsSubmenu = true,
        OnClicked = clicked => clicked.OpenSubmenu(new List<MenuItem> { new() { Name = "MT", OnClicked = _ => {} } }),
    });
};
```
`MenuItem` also has `Prefix` (`SeIconChar?`, e.g. `SeIconChar.BoxedLetterA`), `Priority`, `IsEnabled`,
`UseDefaultPrefix`. `OpenSubmenu` takes `IReadOnlyList<IMenuItem>` (a `List<MenuItem>` works).

## Chat (`IChatGui`) – part of v1.0.0 (reported working)

```csharp
Service.ChatGui.ChatMessage += (IHandleableChatMessage m) => {
    if (m.LogKind is not (XivChatType.Party or XivChatType.CrossParty)) return;
    var text = m.Message.TextValue;
    var sender = m.Sender.Payloads.OfType<PlayerPayload>().FirstOrDefault(); // null for your own messages
    // sender.PlayerName, sender.World.RowId
};
```
Also on the interface: `CheckMessageHandled`, `ChatMessageHandled`, `ChatMessageUnhandled`, `LogMessage`,
`Print(string message, string messageTag, ushort? tagColor)`, `PrintError(...)`.
Toasts: `IToastGui.ShowQuest(string, new QuestToastOptions { DisplayCheckmark = true })`.

## SeString – verified by byte round-trip in a scratch console app

- Build styled text with **Lumina's** `Lumina.Text.SeStringBuilder` (alias it; Dalamud has its own builder):
  `PushColorType(uiColorRow)/PopColorType()`, `PushEdgeColorType/PopEdgeColorType`,
  `PushColorRgba(r,g,b,a)/PopColor()`, `PushEdgeColorRgba`, `Append(string)`, `ToArray()`,
  `ToReadOnlySeString()`.
- Convert for Dalamud APIs: `SeString.Parse(bytes)`. colortype → `UIForegroundPayload`, edgecolortype →
  `UIGlowPayload`, `<color(...)>` → `RawPayload`; `new SeString(payloads).Encode()` gives back identical bytes.
- Debug: `new ReadOnlySeString(bytes).ToMacroString()` → `<edgecolortype(51)><colortype(37)>…`.
  (`ToString()` prints only the raw text.)
- Payloads in Dalamud's `SeString` are mutable objects. As a precaution (not a bug that was observed), cached
  payloads are handed out wrapped in a fresh list: `new SeString(new List<Payload>(cached))`.
- Boxed letter glyphs in the game font: `'A'..'Z' → ch + 0xE030`, `'0'..'9' → ch + 0xE05F`.
- Role colours used by xivPartyIcons (UIColor rows): tank 37, healer 42, melee 524, ranged 32, edge/glow 51.
- `ReadOnlySeString` (Lumina sheet strings) → `.ExtractText()` for plain text.

## Game data (Lumina) and config

- `Service.DataManager.GetExcelSheet<ClassJob>()` (namespace `Lumina.Excel.Sheets`); `row.RowId`,
  `row.Abbreviation.ExtractText()`, `row.Role` (byte: 1 tank, 2 melee, 3 ranged, 4 healer – consistent with
  xivPartyIcons' mapping), `GetRowOrDefault(id)` returns nullable.
- `UIColor` sheet: `Dark`, `Light`, `ClassicFF`, `ClearBlue` (uint RGBA, R in the top byte). Which column
  matches `SystemConfigOption.ColorThemeType` 0..3 is **(unverified)**.
- Icons for ImGui: `Service.TextureProvider.GetFromGameIcon(new GameIconLookup(iconId)).GetWrapOrEmpty()`,
  then `ImGui.Image(wrap.Handle, size)`. Call every frame; don't dispose shared textures.
- `IGameConfig.TryGet(SystemConfigOption | UiConfigOption | UiControlOption, out uint|bool|float|string)`.

## Party and player data

- `GroupManager.Instance()->MainGroup`: `MemberCount`, `PartyMembers[i]` (`ContentId`, `EntityId`,
  `ClassJob` (byte), `HomeWorld` (ushort), `NameString`), `IsEntityIdInParty(entityId)`.
  Out-of-range members report `ClassJob == 0` temporarily – remember the last known job.
- `AgentHUD.Instance()->PartyMembers[i]` (`ContentId`, `EntityId`, `Index` = position on the party list),
  bounded by `PartyMemberCount`.
- Cross-world parties are not in `GroupManager` outside instances;
  `ICondition[ConditionFlag.ParticipatingInCrossWorldPartyOrAlliance]` tells you you're in one.
- In-duty check used: `Condition.Any(ConditionFlag.BoundByDuty, ConditionFlag.BoundByDuty56,
  ConditionFlag.BoundByDuty95)` **(unverified in game which flags are needed)**.
- `IPlayerCharacter.Address` → `(Character*)` for `CharacterData.ClassJob`, `HomeWorld`;
  `(GameObject*)` for `NameString` (avoids SeString allocation).
- `IClientState.IsPvP` – leave nameplates alone in PvP.

## ImGui (`Dalamud.Bindings.ImGui`, no XML docs – use `tools/api.sh`)

Verified present: `Checkbox(label, ref bool)`, `SliderFloat(label, ref float, min, max, format)`,
`SliderInt`, `ColorEdit4(label, ref Vector4, ImGuiColorEditFlags)`, `Selectable(label, bool)`,
`Button`, `SameLine(offset, spacing)`, `TextUnformatted`, `TextDisabled`, `TextColored(Vector4, text)`,
`AlignTextToFramePadding`, `SetNextItemWidth`, `TableSetupColumn(label, flags, width)`, `TableHeadersRow`,
`TableNextColumn`, `IsItemHovered`, `IsAnyItemActive`, `PushTextWrapPos/PopTextWrapPos`, `GetFrameHeight`,
`GetIO().FontGlobalScale`, `Separator`, `Spacing`, `Image(ImTextureID, Vector2)`.
**Missing:** `SeparatorText`. Labels are `ImU8String`; a plain `string` converts implicitly.
