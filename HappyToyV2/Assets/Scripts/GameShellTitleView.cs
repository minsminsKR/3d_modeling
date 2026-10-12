using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace HappyToy.V2
{
    public sealed partial class GameShellView
    {
        Label titleSelectionHint;
        Color TitleAccent => shell.HighContrast ? new Color(1f, .43f, .34f) : new Color(.78f, .27f, .23f);
        Color TitleInk => shell.HighContrast ? Color.white : new Color(.94f, .92f, .87f);
        Color TitleMuted => shell.HighContrast ? new Color(.88f, .88f, .88f) : new Color(.64f, .65f, .65f);

        // A few coloured UI quads shade the borrowed art, without a new bitmap,
        // aspect-dependent stretching, pointer interception or gameplay overlay.
        sealed class CinematicMenuScrim : VisualElement
        {
            readonly bool contrast;
            public CinematicMenuScrim(bool highContrast)
            {
                contrast = highContrast; pickingMode = PickingMode.Ignore; focusable = false;
                generateVisualContent += Draw;
            }
            void Draw(MeshGenerationContext context)
            {
                float w = contentRect.width, h = contentRect.height;
                if (w <= 0 || h <= 0) return;
                var mesh = context.Allocate(32, 48);
                ushort first = 0;
                void Quad(float x0, float y0, float x1, float y1, Color a, Color b, Color c, Color d)
                {
                    mesh.SetNextVertex(new Vertex { position = new Vector3(x0, y0, Vertex.nearZ), tint = a });
                    mesh.SetNextVertex(new Vertex { position = new Vector3(x1, y0, Vertex.nearZ), tint = b });
                    mesh.SetNextVertex(new Vertex { position = new Vector3(x1, y1, Vertex.nearZ), tint = c });
                    mesh.SetNextVertex(new Vertex { position = new Vector3(x0, y1, Vertex.nearZ), tint = d });
                    mesh.SetNextIndex(first); mesh.SetNextIndex((ushort)(first + 1)); mesh.SetNextIndex((ushort)(first + 2));
                    mesh.SetNextIndex((ushort)(first + 2)); mesh.SetNextIndex((ushort)(first + 3)); mesh.SetNextIndex(first);
                    first += 4;
                }
                Color Shade(float alpha) => new Color(.012f, .014f, .018f, alpha);
                float[] stops = { 0, .18f, .38f, .58f, .79f, 1 };
                float[] alpha = contrast ? new[] { .97f, .95f, .85f, .56f, .21f, .17f } : new[] { .78f, .71f, .43f, .16f, .03f, .04f };
                for (int i = 0; i < 5; i++)
                    Quad(w * stops[i], 0, w * stops[i + 1], h, Shade(alpha[i]), Shade(alpha[i + 1]), Shade(alpha[i + 1]), Shade(alpha[i]));
                Quad(0, 0, w, h * .20f, Shade(.34f), Shade(.34f), Shade(0), Shade(0));
                Quad(0, h * .51f, w, h * .84f, Shade(0), Shade(0), Shade(.76f), Shade(.76f));
                Quad(0, h * .84f, w, h, Shade(.76f), Shade(.76f), Shade(.97f), Shade(.97f));
            }
        }

        Button TitleButton(string id, string title, string description, float y, Action action, float x = 70, float width = 395, float height = 42)
        {
            var button = new Button(action) { name = id, text = title, tooltip = description };
            Place(button, x, y, width, height);
            button.style.fontSize = shell.LargeText ? 23 : 20;
            button.style.unityTextAlign = TextAnchor.MiddleLeft;
            button.style.paddingLeft = 26; button.style.paddingRight = 30;
            button.style.paddingTop = button.style.paddingBottom = 0;
            button.style.backgroundColor = Color.clear;
            button.style.color = TitleMuted;
            button.style.borderTopLeftRadius = button.style.borderTopRightRadius = 0;
            button.style.borderBottomLeftRadius = button.style.borderBottomRightRadius = 0;
            Outline(button, new Color(.69f, .66f, .62f, .45f), 1);
            var marker = new VisualElement { name = id + "-selection", pickingMode = PickingMode.Ignore };
            float markerHeight = Mathf.Max(8, height - 32);
            marker.style.position = Position.Absolute; marker.style.left = 0; marker.style.top = (height - markerHeight) * .5f;
            marker.style.width = 3; marker.style.height = markerHeight;
            marker.style.backgroundColor = TitleAccent; marker.style.opacity = 0;
            button.Add(marker);
            var arrow = new Label("›") { pickingMode = PickingMode.Ignore, focusable = false };
            arrow.style.position = Position.Absolute; arrow.style.right = 15; arrow.style.top = 0;
            arrow.style.width = 24; arrow.style.height = height; arrow.style.fontSize = height < 40 ? 18 : 28;
            arrow.style.paddingLeft = arrow.style.paddingRight = arrow.style.paddingTop = arrow.style.paddingBottom = 0;
            arrow.style.marginLeft = arrow.style.marginRight = arrow.style.marginTop = arrow.style.marginBottom = 0;
            arrow.style.unityTextAlign = TextAnchor.MiddleCenter; arrow.style.color = TitleInk; arrow.style.opacity = 0;
            button.Add(arrow);
            bool focused = false;
            float amount = 0;
            void Paint()
            {
                bool active = button.enabledInHierarchy && focused;
                float goal = active ? 1 : 0;
                amount = shell.ReducedMotion ? goal : Mathf.MoveTowards(amount, goal, .14f);
                button.style.backgroundColor = new Color(.25f, .15f, .13f, amount * (shell.HighContrast ? .95f : .72f));
                button.style.paddingLeft = 26 + 10 * amount;
                button.style.color = Color.Lerp(TitleMuted, TitleInk, amount);
                marker.style.opacity = active ? 1 : 0;
                arrow.style.opacity = active ? 1 : 0;
                if (active && titleSelectionHint != null) titleSelectionHint.text = description;
            }
            button.RegisterCallback<FocusInEvent>(_ => { focused = true; Paint(); });
            button.RegisterCallback<FocusOutEvent>(_ => { focused = false; Paint(); });
            // Follow real mouse movement, not synthetic enter events from a
            // viewport resize. Mouse, keyboard and pad share one actual focus.
            button.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (e.deltaPosition.sqrMagnitude > .01f && button.enabledInHierarchy) button.Focus();
            });
            button.schedule.Execute(Paint).Every(16);
            return button;
        }

        void BuildCinematicTitle()
        {
            titleSelectionHint = null;
            bool saved = session.Suspension.HasRun, schoolSaved = session.ChapterSuspension.HasRun;
            var chapter = Text("챕터 1", 70, 72, 540, 42, shell.LargeText ? 25 : 22);
            chapter.style.color = TitleInk;
            var heading = Text("' 잊힌 회랑 '", 66, 120, 650, 88, shell.LargeText ? 49 : 45);
            heading.name = "title-brand"; heading.style.color = TitleInk;
            Panel(70, 224, 565, 1, new Color(.75f, .70f, .67f, .4f));
            titleSelectionHint = Text("남겨진 기억의 흔적을 따라, 돌아갈 문을 찾으세요.\n다섯 기억은 붉은 교실의 제단으로 이어집니다.", 70, 256, 570, 106, shell.LargeText ? 23 : 20);
            titleSelectionHint.name = "title-introduction"; titleSelectionHint.style.color = TitleInk;
            float next = 500;
            if (saved)
            {
                TitleButton("continue-run", "회랑 계속 · 기억 " + session.Suspension.Snapshot.recovered.Count(value => value) + "/5", "중단한 회랑으로 돌아갑니다. 회수한 기억 " + session.Suspension.Snapshot.recovered.Count(value => value) + "/5", next, shell.ContinueCorridor)
                    .SetEnabled(session.Suspension.Writable); next += 48;
            }
            if (schoolSaved)
            {
                TitleButton("continue-chapter", $"폐교 계속 · 기억 {session.ChapterSuspension.Snapshot.recovered}/5", $"중단한 학교 탐색으로 돌아갑니다. 회수한 기억 {session.ChapterSuspension.Snapshot.recovered}/5", next, shell.ContinueChapter)
                    .SetEnabled(session.ChapterSuspension.Writable); next += 48;
            }
            if (!saved && !schoolSaved)
            {
                TitleButton("continue-unavailable", "계속 · 저장된 탐색 없음", "저장된 탐색이 있으면 이곳에서 이어갈 수 있습니다.", next, () => { }).SetEnabled(false); next += 48;
            }
            TitleButton("begin", "새 회랑 시작", "남겨진 기억의 흔적을 따라, 돌아갈 문을 찾으세요.\n다섯 기억은 붉은 교실의 제단으로 이어집니다.", next, shell.BeginCorridorCinematic); next += 48;
            TitleButton("begin-school", "폐교의 기억", "세 층의 폐교를 탐색하며, 마지막 출석의 흔적을 찾으세요.", next, shell.BeginChapter); next += 48;
            TitleButton("settings", "설정", "화면과 조작, 사운드 및 접근성 옵션을 조정합니다.", next, shell.Settings); next += 48;
            TitleButton("school-records", "탐색 기록", "최근 학교 탐색의 기록을 확인합니다.", next, shell.ChapterHistory); next += 48;
            TitleButton("quit", "종료", "Happy Toy를 종료합니다.", next, () => Application.Quit());
            bool protectedRecords = !session.ChapterRecords.Writable && !string.IsNullOrEmpty(session.ChapterRecords.Status);
            bool protectedSchool = !session.ChapterSuspension.Writable && !string.IsNullOrEmpty(session.ChapterSuspension.Status);
            bool protectedCorridor = !session.Suspension.Writable && !string.IsNullOrEmpty(session.Suspension.Status);
            if (protectedRecords || protectedSchool || protectedCorridor)
            {
                string mode = protectedSchool && protectedCorridor ? "회랑·폐교" : protectedSchool ? "폐교" : "회랑";
                string warning = protectedSchool || protectedCorridor ? mode + " 중단 기록을 읽을 수 없어 원문을 보존했습니다.\n새 탐색은 가능하며, 해당 중단 저장은 보호 중입니다." : "학교 기록 저장이 중지됐습니다.\n탐색 기록에서 상태를 확인하세요.";
                var notice = Text(warning, 70, 380, 720, 100, shell.LargeText ? 21 : 18);
                notice.name = protectedRecords ? "school-profile-notice" : "checkpoint-status";
                notice.style.color = new Color(.98f, .67f, .53f);
            }
            // Keep separate profile summaries available to history/data consumers;
            // the title's visual focus is its chapter, navigation and real creature.
            var corridor = session.Records.Snapshot; var school = session.ChapterRecords.Snapshot;
            var first = Text($"회랑 탐색 {corridor.attempts}회 · 탈출 {corridor.escapes}회\n최고 기억 {corridor.bestRecovered}/5", 0, 0, 1, 1);
            first.name = "corridor-record-summary"; first.style.display = DisplayStyle.None;
            var second = Text($"학교 탐색 {school.attempts}회 · 탈출 {school.escapes}회\n최고 기억 {school.bestRecovered}/5", 0, 0, 1, 1);
            second.name = "school-record-summary"; second.style.display = DisplayStyle.None;
        }
    }
}
