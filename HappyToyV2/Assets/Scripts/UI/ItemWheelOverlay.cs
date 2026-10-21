using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace HappyToy.V2
{
    // A presentation-only radial wheel. Counts, selected item and accessibility
    // preferences arrive as a snapshot; this view never changes game time.
    internal sealed class ItemWheelOverlay : VisualElement
    {
        public static readonly string[] Names = { "콩알탄", "덫", "향", "부적", "손전등" };
        static readonly string[] IconPaths = { "Hud/Items/snapper", "Hud/Items/trap", "Hud/Items/incense", "Hud/Items/talisman", FlashlightGauge.IconResourcePath };
        static readonly string[] Descriptions = {
            "소리가 난 위치로\n괴물을 유인합니다.",
            "밟은 괴물을 그 자리에서\n5초간 붙잡습니다.",
            "남은 기억으로 흐르는 연기\n모두 모으면 제단을 향합니다.",
            "타는 동안 10초간 괴물 정지\n괴물에 닿아도 안전합니다.",
            "손전등의 빛을 켜거나 끕니다." };
        readonly VisualElement wheel;
        readonly Label title, count, detail, hint, eyebrow, equipped;
        readonly VisualElement[] slots = new VisualElement[PlayerItemLoadout.SlotCount];
        readonly Image[] icons = new Image[PlayerItemLoadout.SlotCount];
        readonly Label[] labels = new Label[PlayerItemLoadout.SlotCount], counts = new Label[PlayerItemLoadout.SlotCount];
        readonly int[] previousStock = new int[PlayerItemLoadout.SlotCount];
        int selected, equippedItem, fragments;
        bool highContrast, largeText, open, gamepad, presented, unlimited;
        Color Paper => highContrast ? Color.white : new Color(.94f, .91f, .82f);
        Color Gold => highContrast ? new Color(1, .88f, .53f) : new Color(.93f, .77f, .45f);

        public ItemWheelOverlay()
        {
            name = "item-selection-overlay"; pickingMode = PickingMode.Ignore; focusable = false;
            Fill(this);
            wheel = new VisualElement { name = "item-wheel", pickingMode = PickingMode.Ignore, focusable = false };
            Fill(wheel); Add(wheel);
            wheel.generateVisualContent += Draw;
            wheel.RegisterCallback<GeometryChangedEvent>(_ => PlaceElements());
            eyebrow = Text(wheel, "item-wheel-heading"); eyebrow.text = "소지품";
            title = Text(wheel, "item-wheel-title");
            count = Text(wheel, "item-wheel-availability");
            detail = Text(wheel, "item-wheel-description");
            hint = Text(wheel, "item-wheel-hint");
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i] = new VisualElement { name = "item-wheel-slot-" + i, pickingMode = PickingMode.Ignore, focusable = false };
                slot.style.position = Position.Absolute; wheel.Add(slot);
                var texture = Resources.Load<Texture2D>(IconPaths[i]);
                if (!texture) throw new InvalidOperationException("Missing authored item wheel icon: " + IconPaths[i]);
                icons[i] = new Image { name = "item-wheel-icon-" + i, image = texture, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                icons[i].style.position = Position.Absolute; slot.Add(icons[i]);
                labels[i] = Text(slot, "item-wheel-name-" + i); labels[i].text = Names[i];
                counts[i] = Text(slot, "item-wheel-count-" + i);
            }
            equipped = Text(this, "selected-item");
            equipped.style.left = StyleKeyword.Auto;
            equipped.style.right = 40; equipped.style.bottom = 90;
            equipped.style.unityTextAlign = TextAnchor.MiddleRight;
            equipped.style.backgroundColor = new Color(.012f, .019f, .015f, .7f);
            equipped.style.paddingLeft = equipped.style.paddingRight = 12;
            equipped.style.paddingTop = equipped.style.paddingBottom = 7;
        }
        static void Fill(VisualElement element)
        {
            element.style.position = Position.Absolute;
            element.style.left = element.style.right = element.style.top = element.style.bottom = 0;
        }
        static Label Text(VisualElement parent, string name)
        {
            var label = new Label { name = name, pickingMode = PickingMode.Ignore, focusable = false };
            label.style.position = Position.Absolute;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginLeft = label.style.marginRight = label.style.marginTop = label.style.marginBottom = 0;
            label.style.paddingLeft = label.style.paddingRight = label.style.paddingTop = label.style.paddingBottom = 0;
            parent.Add(label); return label;
        }
        public void Present(int choice, int equippedChoice, int[] stock, int fragmentCount, bool usingGamepad, bool isOpen, bool contrast, bool largerText, bool infiniteItems)
        {
            bool changed = !presented || choice != selected || equippedChoice != equippedItem || fragmentCount != fragments ||
                usingGamepad != gamepad || isOpen != open || contrast != highContrast || largerText != largeText || infiniteItems != unlimited;
            for (int i = 0; i < previousStock.Length; i++) changed |= stock[i] != previousStock[i];
            if (!changed) return;
            selected = choice; equippedItem = equippedChoice; fragments = fragmentCount;
            unlimited = infiniteItems;
            gamepad = usingGamepad; open = isOpen; highContrast = contrast; largeText = largerText; presented = true;
            Array.Copy(stock, previousStock, previousStock.Length);
            wheel.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            equipped.style.display = open ? DisplayStyle.None : DisplayStyle.Flex;
            wheel.style.backgroundColor = new Color(.008f, .014f, .011f, highContrast ? .7f : .46f);
            title.text = Names[choice]; title.style.color = Paper;
            count.text = unlimited ? choice == (int)PlayerItem.Flashlight ? "배터리 무한 ∞" : "소지품 무한 ∞" : Availability(choice, stock[choice], fragments); count.style.color = unlimited || stock[choice] > 0 ? Gold : Paper;
            detail.text = Descriptions[choice]; detail.style.color = Paper;
            eyebrow.style.color = Gold; hint.style.color = Paper;
            hint.text = gamepad ? "오른쪽 스틱  선택    RB 놓기  장착    RT  사용" : "마우스  선택    Tab 놓기  장착    왼쪽 클릭  사용";
            string itemStock = unlimited ? "  ∞" : equippedChoice == (int)PlayerItem.Flashlight ? "" : "  "+stock[equippedChoice]+(equippedChoice == 0 ? "발" : "개");
            equipped.text = Names[equippedChoice] + itemStock + (gamepad ? "   [RB]" : "   [Tab]") +
                (fragments > 0 ? "\n부적 조각  "+fragments+" / 5" : "");
            equipped.style.color = Paper;
            equipped.style.fontSize = largeText ? 24 : 20;
            equipped.style.width = largeText ? 460 : 400;
            equipped.style.height = fragments > 0 ? (largeText ? 90 : 80) : (largeText ? 56 : 50);
            for (int i = 0; i < slots.Length; i++)
            {
                bool available = unlimited || stock[i] > 0;
                Color colour = i == choice ? Gold : available ? Paper : highContrast ? new Color(.8f, .8f, .76f) : new Color(.64f, .66f, .6f);
                labels[i].style.color = colour; counts[i].style.color = colour;
                counts[i].text = unlimited ? "∞" : i == 4 ? "" : stock[i] == 0 ? "없음" : stock[i]+(i == 0 ? "발" : "개");
                icons[i].tintColor = available || i == choice ? Color.white : new Color(.56f, .58f, .54f);
                labels[i].style.unityFontStyleAndWeight = i == choice ? FontStyle.Bold : FontStyle.Normal;
            }
            PlaceElements(); wheel.MarkDirtyRepaint();
        }
        static string Availability(int item, int stock, int fragments)
        {
            if (item == (int)PlayerItem.Flashlight) return "";
            if (item == (int)PlayerItem.Talisman) return stock > 0 ? "완성 부적  "+stock+"개  ·  조각 "+fragments+" / 5" : "부적 조각  "+fragments+" / 5";
            if (stock == 0) return "가지고 있지 않습니다";
            if (item == (int)PlayerItem.Snapper) return "현재 묶음  "+SnapperStockRules.CurrentPack(stock)+" / 6  ·  여분 "+SnapperStockRules.ReservePacks(stock)+"묶음";
            return "소지  "+stock+"개";
        }
        float OuterRadius => Mathf.Min(wheel.contentRect.width * .4f, wheel.contentRect.height * .365f);
        void PlaceElements()
        {
            var rect = wheel.contentRect;
            if (rect.width <= 0 || rect.height <= 0) return;
            var centre = rect.center;
            float outer = OuterRadius, inner = outer * .51f, radius = outer * .76f;
            float slotWidth = outer * .48f, slotHeight = largeText ? 164 : 148, iconSize = largeText ? 75 : 68;
            for (int i = 0; i < slots.Length; i++)
            {
                float angle = i * Mathf.PI * 2 / slots.Length;
                slots[i].style.left = centre.x + Mathf.Sin(angle) * radius - slotWidth * .5f;
                slots[i].style.top = centre.y - Mathf.Cos(angle) * radius - slotHeight * .5f;
                slots[i].style.width = slotWidth; slots[i].style.height = slotHeight;
                icons[i].style.left = (slotWidth - iconSize) * .5f; icons[i].style.top = 0;
                icons[i].style.width = icons[i].style.height = iconSize;
                PlaceText(labels[i], 0, iconSize + 3, slotWidth, largeText ? 42 : 36, largeText ? 25 : 22);
                PlaceText(counts[i], 0, iconSize + (largeText ? 45 : 39), slotWidth, largeText ? 38 : 34, largeText ? 22 : 19);
            }
            float centreWidth = inner * 1.82f;
            // Korean fallback fonts have a taller line box than their nominal
            // font size; leave room for it at every panel scale and text size.
            PlaceText(title, centre.x - centreWidth * .5f, centre.y - 100, centreWidth, largeText ? 58 : 52, largeText ? 35 : 31);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            PlaceText(count, centre.x - centreWidth * .5f, centre.y - 40, centreWidth, largeText ? 68 : 64, largeText ? 21 : 18);
            PlaceText(detail, centre.x - centreWidth * .5f, centre.y + 36, centreWidth, largeText ? 112 : 98, largeText ? 23 : 20);
            PlaceText(eyebrow, 0, Mathf.Max(22, centre.y - outer - 58), rect.width, 40, largeText ? 23 : 20);
            PlaceText(hint, 20, Mathf.Min(rect.height - 53, centre.y + outer + 26), rect.width - 40, 40, largeText ? 23 : 20);
        }
        static void PlaceText(Label label, float x, float y, float width, float height, int fontSize)
        {
            label.style.left = x; label.style.top = y; label.style.width = width; label.style.height = height; label.style.fontSize = fontSize;
        }
        void Draw(MeshGenerationContext context)
        {
            if (!open) return;
            var painter = context.painter2D; var centre = wheel.contentRect.center;
            float outer = OuterRadius, inner = outer * .51f;
            for (int slot = 0; slot < slots.Length; slot++)
            {
                float start = (slot - .5f) * 360 / slots.Length - 90 + 1, end = (slot + .5f) * 360 / slots.Length - 90 - 1;
                painter.BeginPath();
                for (int j = 0; j <= 25; j++)
                {
                    float a = Mathf.Lerp(start, end, j / 25f) * Mathf.Deg2Rad;
                    var point = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * outer;
                    if (j == 0) painter.MoveTo(point); else painter.LineTo(point);
                }
                for (int j = 25; j >= 0; j--)
                {
                    float a = Mathf.Lerp(start, end, j / 25f) * Mathf.Deg2Rad;
                    painter.LineTo(centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * inner);
                }
                painter.ClosePath();
                painter.fillColor = slot == selected ? new Color(.24f, .19f, .1f, .98f) : new Color(.027f, .042f, .034f, highContrast ? 1 : .96f);
                painter.Fill();
                painter.lineWidth = slot == selected ? (highContrast ? 3 : 2) : 1;
                painter.strokeColor = slot == selected ? Gold : new Color(.5f, .53f, .43f, highContrast ? .8f : .45f);
                painter.Stroke();
            }
            float direction = selected * Mathf.PI * 2 / slots.Length - Mathf.PI * .5f;
            Vector2 along = new Vector2(Mathf.Cos(direction), Mathf.Sin(direction)), across = new Vector2(-along.y, along.x);
            painter.BeginPath(); painter.MoveTo(centre + along * (inner + 4));
            painter.LineTo(centre + along * (inner + 17) + across * 7);
            painter.LineTo(centre + along * (inner + 17) - across * 7);
            painter.ClosePath(); painter.fillColor = Gold; painter.Fill();
        }
    }
}
