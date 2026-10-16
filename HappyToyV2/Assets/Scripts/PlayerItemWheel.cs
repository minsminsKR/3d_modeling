using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace HappyToy.V2
{
    // Coordinates input and selection. GameShell owns time/cursor and the UI
    // receives only a presentation snapshot, never inventory mutations.
    [DefaultExecutionOrder(-40)]
    [DisallowMultipleComponent]
    public sealed class PlayerItemWheel : MonoBehaviour
    {
        PlayerMotor player;
        GameShell owner;
        ItemWheelOverlay overlay;
        readonly int[] stock = new int[PlayerItemLoadout.SlotCount];
        bool requireRelease = true, ownedOpen;
        int choice;
        public int Highlighted => choice;

        void Awake() { player = GetComponent<PlayerMotor>(); }
        void Update()
        {
            var session = GameSession.Current;
            if (!session || !player || !player.Items || !player.Rituals) return;
            var shell = session.Shell;
            if (!shell) return;
            bool held = PlayerControls.ItemWheelHeld;
            bool allowed = session.InputAllowed && !player.Hidden && !player.Rituals.Burning &&
                (!session.ChapterMode || !session.Chapter.FirstAppearances.CameraOwned) && PlayerControls.AcceptsInput;
            if (!allowed)
            {
                if (shell.ItemWheelOpen) shell.SetItemWheelOpen(false);
                ownedOpen = false;
                requireRelease = true;
                return;
            }
            // An external page/focus transition cancels instead of committing
            // the highlighted item. A still-held key cannot reopen the wheel.
            if (ownedOpen && !shell.ItemWheelOpen) { ownedOpen = false; requireRelease = true; }
            if (!held) requireRelease = false;
            if (held && !requireRelease && !shell.ItemWheelOpen && shell.GameplayEntryFrame != Time.frameCount)
            {
                choice = (int)player.Items.Selected;
                owner = shell;
                shell.SetItemWheelOpen(true);
                ownedOpen = shell.ItemWheelOpen;
                player.Firecrackers.Aim?.Cancel();
                if (!DiagnosticDesktopPolicy.SuppressCursorWrites && !PlayerControls.UsingGamepad && Mouse.current != null)
                    Mouse.current.WarpCursorPosition(new Vector2(Screen.width, Screen.height) * .5f);
            }
            if (!held && shell.ItemWheelOpen)
            {
                player.Items.Select((PlayerItem)choice);
                shell.SetItemWheelOpen(false);
                ownedOpen = false;
            }
            if (!shell.ItemWheelOpen) return;
            Vector2 direction;
            if (PlayerControls.UsingGamepad && Gamepad.current != null)
                direction = Gamepad.current.rightStick.ReadValue();
            else if (Mouse.current != null)
                direction = (Mouse.current.position.ReadValue() - new Vector2(Screen.width, Screen.height) * .5f) /
                    Mathf.Max(1, Mathf.Min(Screen.width, Screen.height) * .20f);
            else direction = Vector2.zero;
            choice = ItemWheelRules.Select(direction.x, direction.y, PlayerItemLoadout.SlotCount, choice);
        }

        // Called by the shell view after its panel/page tree has been rebuilt.
        public void RefreshPresentation(VisualElement root, bool highContrast, bool largeText)
        {
            var session = GameSession.Current;
            if (root == null || !session || !player || !player.Items || !player.Rituals) return;
            if (overlay == null) overlay = new ItemWheelOverlay();
            if (overlay.parent != root) root.Add(overlay);
            overlay.style.display = session.InputAllowed ? DisplayStyle.Flex : DisplayStyle.None;
            if (!session.InputAllowed) return;
            for (int i = 0; i < stock.Length; i++) stock[i] = player.Items.Count((PlayerItem)i);
            overlay.Present(choice, (int)player.Items.Selected, stock, player.Rituals.Fragments,
                PlayerControls.UsingGamepad, session.Shell.ItemWheelOpen, highContrast, largeText);
        }
        void OnApplicationFocus(bool focused) { if (!focused) requireRelease = true; }
        void OnDisable()
        {
            var session = GameSession.Current;
            if (ownedOpen && owner && session && session.player == player && session.Shell == owner)
                owner.SetItemWheelOpen(false);
            overlay?.RemoveFromHierarchy();
            requireRelease = true;
            ownedOpen = false;
        }
    }
}
