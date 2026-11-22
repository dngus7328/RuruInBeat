using UnityEngine;

/// <summary>
/// Shared references to the project's existing, user-authored TitleUI sprites.
/// These assets remain in Assets/Image/TitleUI and keep their original GUIDs.
///
/// The theme asset lives under Resources so runtime-created screens can reuse
/// the same illustrations without duplicating or restyling any PNG.
/// </summary>
[CreateAssetMenu(menuName = "Ruru In Beat/Original UI Theme",
    fileName = "RuruOriginalUITheme")]
public sealed class RuruOriginalUITheme : ScriptableObject
{
    [Header("Existing TitleUI artwork - no generated replacements")]
    public Sprite titleButton;
    public Sprite selectedCategory;
    public Sprite confirmationPanel;
}
