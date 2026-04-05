using System;
using System.Collections.Generic;
using SkiaSharp;

namespace QSBT1_Streamdeck.QSApi
{
    public class TuneDialRenderer
    {
        private const int W       = 200;
        private const int H       = 100;
        private const int HeaderH = 22;

        private static SKColor BgColor     = new SKColor(18,  18,  18);
        private static SKColor HeaderBg    = new SKColor(30,  30,  30);
        private static SKColor BarBg       = new SKColor(50,  50,  50);
        private static SKColor OnColor     = new SKColor(0x5D,0xB3,0xE2); // bleu
        private static SKColor OffColor    = new SKColor(0xFD,0x89,0x0D); // orange
        private static SKColor TextColor   = new SKColor(180, 180, 180);
        private static SKColor SepColor    = new SKColor(45,  45,  45);
        private static SKColor DimColor    = new SKColor(100, 100, 100);

        public record ParamDisplay(string Label, double Value, double Min, double Max, bool Enabled, bool Selected = false);

        // ── Couleurs selon état ON/OFF ────────────────────────────────────────
        // Actif   = orange plein
        // Inactif = bleu vide (cercle)
        private static SKColor TitleColor(bool enabled) => enabled ? OffColor : OnColor;
        private static SKColor DotColor(bool enabled)   => enabled ? OffColor : OnColor;

        // ── Helper barre — gère les ranges négatifs ───────────────────────────
        // Si min < 0 : zéro au centre, marqueur vertical, barre gauche/droite
        // Si min >= 0 : barre classique de gauche à droite
        private static void DrawBar(SKCanvas canvas, SKColor barCol, SKColor barBgCol,
                                    float barX, float barY, float barW, float barH,
                                    double value, double min, double max, float radius = 2f)
        {
            // Fond
            using var barBgPaint = new SKPaint { Color = barBgCol };
            canvas.DrawRoundRect(barX, barY, barW, barH, radius, radius, barBgPaint);

            double range = max - min;
            if (range <= 0) return;

            using var fillPaint   = new SKPaint { Color = barCol, IsAntialias = true };
            using var markerPaint = new SKPaint { Color = barCol, IsAntialias = true };

            if (min < 0)
            {
                // Range négatif : zéro au centre
                float centerX   = barX + barW / 2f;
                double norm     = Math.Clamp(value / max, -1, 1); // -1..+1
                float  fillW    = (float)(barW / 2f * Math.Abs(norm));

                if (fillW > 0.5f)
                {
                    if (value > 0)
                        canvas.DrawRoundRect(centerX, barY, fillW, barH, radius, radius, fillPaint);
                    else
                        canvas.DrawRoundRect(centerX - fillW, barY, fillW, barH, radius, radius, fillPaint);
                }

                // Marqueur central vertical (dépasse 50% en haut/bas, large 25% hauteur)
                float mW = Math.Max(1.5f, barH * 0.25f);
                float mH = barH * 2f;           // dépasse 50% de chaque côté = hauteur totale x2
                float mY = barY - barH * 0.5f;  // centré sur la barre
                canvas.DrawRect(centerX - mW / 2f, mY, mW, mH, markerPaint);
            }
            else
            {
                // Range positif classique
                double norm  = Math.Clamp((value - min) / range, 0, 1);
                float  fillW = (float)(barW * norm);
                if (fillW > 0)
                    canvas.DrawRoundRect(barX, barY, fillW, barH, radius, radius, fillPaint);
            }
        }

        // ── Dial render (200x100) ─────────────────────────────────────────────
        public static string Render(string tuneName, bool tuneEnabled, List<ParamDisplay> parameters)
        {
            var info    = new SKImageInfo(W, H, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info);
            var canvas  = surface.Canvas;
            canvas.Clear(BgColor);

            using var hdrPaint = new SKPaint { Color = HeaderBg };
            canvas.DrawRect(0, 0, W, HeaderH, hdrPaint);

            // Titre : orange si actif, bleu si inactif
            using var hdrFont = new SKPaint { Color = TitleColor(tuneEnabled), TextSize = 11f, IsAntialias = true, FakeBoldText = true };
            canvas.DrawText(tuneName, 4, 15, hdrFont);

            // État ON/OFF
            string stateStr = tuneEnabled ? "● ON" : "○ OFF";
            using var statePaint = new SKPaint { Color = DotColor(tuneEnabled), TextSize = 9f, IsAntialias = true, FakeBoldText = true };
            float stateW = statePaint.MeasureText(stateStr);
            canvas.DrawText(stateStr, W - stateW - 4, 15, statePaint);

            int count = Math.Min(parameters.Count, 3);
            if (count == 0) return Encode(surface);

            float rowH  = (H - HeaderH) / (float)count;
            float barHt = 10f;

            using var sepPaint   = new SKPaint { Color = SepColor };

            using var measurePaint = new SKPaint { TextSize = 9f, IsAntialias = true };
            float maxLabelW = 0;
            foreach (var p in parameters) { float w = measurePaint.MeasureText(p.Label); if (w > maxLabelW) maxLabelW = w; }
            float labelEndX = maxLabelW + 6;

            for (int i = 0; i < count; i++)
            {
                var   p   = parameters[i];
                float ry  = HeaderH + i * rowH;
                canvas.DrawLine(0, ry, W, ry, sepPaint);

                SKColor labelCol = tuneEnabled ? (p.Selected ? OffColor : OnColor) : (p.Selected ? OffColor : DimColor);
                SKColor barCol   = tuneEnabled ? (p.Selected ? OffColor : OnColor) : new SKColor(80, 80, 80);

                using var labelPaint = new SKPaint { Color = labelCol, TextSize = 9f, IsAntialias = true, FakeBoldText = p.Selected };
                float lw = labelPaint.MeasureText(p.Label);
                canvas.DrawText(p.Label, labelEndX - lw, ry + 13, labelPaint);

                string valStr = FormatVal(p.Value);
                using var valPaint = new SKPaint { Color = TextColor, TextSize = 9f, IsAntialias = true, FakeBoldText = true };
                float valW = valPaint.MeasureText(valStr);
                canvas.DrawText(valStr, W - valW - 3, ry + 13, valPaint);

                float barX  = labelEndX + 4;
                float barW  = W - barX - valW - 6;
                float barY  = ry + (rowH - barHt) / 2f;

                DrawBar(canvas, barCol, BarBg, barX, barY, barW, barHt, p.Value, p.Min, p.Max);
            }
            return Encode(surface);
        }

        // ── Status/Button render (72x72) ──────────────────────────────────────
        // headerSelected = true si le "curseur" est sur le header (toggle mode)
        public static string RenderStatus(string tuneName, bool tuneEnabled, List<ParamDisplay> parameters, bool headerSelected = false)
        {
            const int SW      = 72;
            const int SH      = 72;
            const int SHdrH   = 18;
            const float SBarH = 4f;

            var info    = new SKImageInfo(SW, SH, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info);
            var canvas  = surface.Canvas;
            canvas.Clear(BgColor);

            // Header bg — surbrillance si headerSelected
            SKColor hdrBg = headerSelected ? new SKColor(60, 35, 0) : HeaderBg;
            using var hdrPaint = new SKPaint { Color = hdrBg };
            canvas.DrawRect(0, 0, SW, SHdrH, hdrPaint);

            // Titre orange si actif, bleu si inactif
            SKColor titleCol = TitleColor(tuneEnabled);
            string shortName = tuneName.Length > 9 ? tuneName[..9] : tuneName;
            using var hdrFont = new SKPaint { Color = titleCol, TextSize = 9f, IsAntialias = true, FakeBoldText = true };
            canvas.DrawText(shortName, 2, 13, hdrFont);

            // Point : orange plein si actif, cercle bleu si inactif
            using var dotPaint = new SKPaint { Color = DotColor(tuneEnabled), IsAntialias = true, IsStroke = !tuneEnabled, StrokeWidth = 1.5f };
            canvas.DrawCircle(SW - 6, 9, 4, dotPaint);

            int count = Math.Min(parameters.Count, 3);
            if (count == 0) return Encode(surface);
            float rowH = (SH - SHdrH) / (float)count;

            using var sepPaint   = new SKPaint { Color = SepColor };

            for (int i = 0; i < count; i++)
            {
                var   p   = parameters[i];
                float ry  = SHdrH + i * rowH;
                bool  sel = p.Selected;

                canvas.DrawLine(0, ry, SW, ry, sepPaint);

                SKColor barCol   = tuneEnabled ? (sel ? OffColor : OnColor) : new SKColor(80, 80, 80);
                SKColor labelCol = tuneEnabled ? (sel ? OffColor : OnColor) : DimColor;

                using var labelPaint = new SKPaint { Color = labelCol, TextSize = 7.5f, IsAntialias = true, FakeBoldText = sel };
                canvas.DrawText(p.Label, 2, ry + 10, labelPaint);

                string valStr = FormatVal(p.Value);
                using var valPaint = new SKPaint { Color = TextColor, TextSize = 7.5f, IsAntialias = true, FakeBoldText = true };
                float valW = valPaint.MeasureText(valStr);
                canvas.DrawText(valStr, SW - valW - 2, ry + 10, valPaint);

                float barY = ry + rowH - SBarH - 1;
                DrawBar(canvas, barCol, BarBg, 0, barY, SW, SBarH, p.Value, p.Min, p.Max, 0f);
            }
            return Encode(surface);
        }

        // ── Single Tune Button render (72x72) ─────────────────────────────────
        // Un seul paramètre affiché : label + grande barre + valeur centrée
        // headerSelected = curseur sur le header (toggle mode)
        public static string RenderSingle(string tuneName, bool tuneEnabled,
                                           string paramLabel, double value,
                                           double min, double max,
                                           bool headerSelected = false)
        {
            const int SW    = 72;
            const int SH    = 72;
            const int SHdrH = 18;

            var info    = new SKImageInfo(SW, SH, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info);
            var canvas  = surface.Canvas;
            canvas.Clear(BgColor);

            // ── Header ────────────────────────────────────────────────────────
            SKColor hdrBg = headerSelected ? new SKColor(60, 35, 0) : HeaderBg;
            using var hdrPaint = new SKPaint { Color = hdrBg };
            canvas.DrawRect(0, 0, SW, SHdrH, hdrPaint);

            string shortName = tuneName.Length > 9 ? tuneName[..9] : tuneName;
            using var hdrFont = new SKPaint { Color = TitleColor(tuneEnabled), TextSize = 9f, IsAntialias = true, FakeBoldText = true };
            canvas.DrawText(shortName, 2, 13, hdrFont);

            using var dotPaint = new SKPaint { Color = DotColor(tuneEnabled), IsAntialias = true, IsStroke = !tuneEnabled, StrokeWidth = 1.5f };
            canvas.DrawCircle(SW - 6, 9, 4, dotPaint);

            // ── Zone param ────────────────────────────────────────────────────
            float bodyY = SHdrH + 2;
            float bodyH = SH - SHdrH - 2;

            SKColor accent = tuneEnabled ? OffColor : new SKColor(80, 80, 80);

            // Label param centré en haut de la zone
            using var labelPaint = new SKPaint { Color = tuneEnabled ? OnColor : DimColor, TextSize = 8f, IsAntialias = true };
            float lw = labelPaint.MeasureText(paramLabel);
            canvas.DrawText(paramLabel, (SW - lw) / 2f, bodyY + 12, labelPaint);

            // Grande barre
            const float barH  = 8f;
            const float barMX = 4f;
            float barW  = SW - barMX * 2;
            float barY  = bodyY + 18;

            DrawBar(canvas, accent, BarBg, barMX, barY, barW, barH, value, min, max);

            // Valeur en grand centrée dessous
            string valStr = FormatVal(value);
            using var valPaint = new SKPaint { Color = tuneEnabled ? OffColor : DimColor, TextSize = 16f, IsAntialias = true, FakeBoldText = true };
            float vw = valPaint.MeasureText(valStr);
            canvas.DrawText(valStr, (SW - vw) / 2f, barY + barH + 18, valPaint);

            return Encode(surface);
        }

        // ── Profile button render (72x72) ─────────────────────────────────────
        public static string RenderProfileButton(string activeName, string nextName, bool connected)
        {
            const int SW    = 72;
            const int SH    = 72;
            const int SHdrH = 18;

            var info    = new SKImageInfo(SW, SH, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info);
            var canvas  = surface.Canvas;
            canvas.Clear(BgColor);

            // ── Header "Profile" ──────────────────────────────────────────────
            using var hdrPaint = new SKPaint { Color = HeaderBg };
            canvas.DrawRect(0, 0, SW, SHdrH, hdrPaint);

            using var hdrFont = new SKPaint { Color = OffColor, TextSize = 9f, IsAntialias = true, FakeBoldText = true };
            canvas.DrawText("Profile", 2, 13, hdrFont);

            using var dotPaint = new SKPaint { Color = OffColor, IsAntialias = true };
            canvas.DrawCircle(SW - 6, 9, 4, dotPaint);

            if (!connected)
            {
                using var dimPaint = new SKPaint { Color = DimColor, TextSize = 8f, IsAntialias = true };
                canvas.DrawText("No device", 2, 40, dimPaint);
                return Encode(surface);
            }

            // ── Séparateur ────────────────────────────────────────────────────
            float sepY = SHdrH + (SH - SHdrH) * 0.55f;
            using var sepPaint = new SKPaint { Color = SepColor };
            canvas.DrawLine(0, sepY, SW, sepY, sepPaint);

            // ── Profil actif — grand orange ───────────────────────────────────
            float activeAreaH = sepY - SHdrH;
            string a = activeName.Length > 9 ? activeName[..9] : activeName;
            using var activePaint = new SKPaint { Color = OffColor, TextSize = 11f, IsAntialias = true, FakeBoldText = true };
            // Réduire si trop long
            while (activePaint.MeasureText(a) > SW - 4 && a.Length > 1)
                a = a[..^1];
            float aw = activePaint.MeasureText(a);
            canvas.DrawText(a, (SW - aw) / 2f, SHdrH + activeAreaH / 2f + 5f, activePaint);

            // ── Next — petit bleu ─────────────────────────────────────────────
            float nextAreaH = SH - sepY;
            float nextMidY  = sepY + nextAreaH / 2f;

            using var nextLabelPaint = new SKPaint { Color = DimColor, TextSize = 7f, IsAntialias = true };
            canvas.DrawText("Next", 2, nextMidY - 2, nextLabelPaint);

            string n = nextName.Length > 9 ? nextName[..9] : nextName;
            using var nextValPaint = new SKPaint { Color = OnColor, TextSize = 8f, IsAntialias = true, FakeBoldText = true };
            float nw = nextValPaint.MeasureText(n);
            canvas.DrawText(n, (SW - nw) / 2f, nextMidY + 9, nextValPaint);

            return Encode(surface);
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private static string Encode(SKSurface surface)
        {
            using var img  = surface.Snapshot();
            using var data = img.Encode(SKEncodedImageFormat.Png, 100);
            return "data:image/png;base64," + Convert.ToBase64String(data.ToArray());
        }

        public static string FormatVal(double v)
        {
            if (Math.Abs(v) >= 100) return v.ToString("F0");
            if (Math.Abs(v) >= 10)  return v.ToString("F1");
            return v.ToString("F2");
        }
    }
}
