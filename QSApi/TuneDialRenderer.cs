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
        // Si min < 0 : zéro au centre, marqueur vertical bleu, barre gauche/droite
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
            // Marqueur zéro toujours en bleu
            using var markerPaint = new SKPaint { Color = OnColor, IsAntialias = true };

            if (min < 0)
            {
                // Range négatif : zéro au centre
                float centerX = barX + barW / 2f;
                double norm   = Math.Clamp(value / max, -1, 1);
                float  fillW  = (float)(barW / 2f * Math.Abs(norm));

                if (fillW > 0.5f)
                {
                    if (value > 0)
                        canvas.DrawRoundRect(centerX, barY, fillW, barH, radius, radius, fillPaint);
                    else
                        canvas.DrawRoundRect(centerX - fillW, barY, fillW, barH, radius, radius, fillPaint);
                }

                // Marqueur central : bleu, réduit de 2px haut et bas
                float mW  = Math.Max(1.5f, barH * 0.25f);
                float mH  = barH * 2f - 4f;        // -2px haut, -2px bas
                float mY  = barY - barH * 0.5f + 2f; // +2px vers le bas (réduit haut)
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
            using var hdrFont = new SKPaint { Color = TitleColor(tuneEnabled), TextSize = 11f, IsAntialias = true };
            canvas.DrawText(tuneName, 4, 15, hdrFont);

            // État ON/OFF
            string stateStr = tuneEnabled ? "● ON" : "○ OFF";
            using var statePaint = new SKPaint { Color = DotColor(tuneEnabled), TextSize = 9f, IsAntialias = true };
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

                using var labelPaint = new SKPaint { Color = labelCol, TextSize = 9f, IsAntialias = true };
                float lw = labelPaint.MeasureText(p.Label);
                canvas.DrawText(p.Label, labelEndX - lw, ry + 13, labelPaint);

                string valStr = FormatVal(p.Value);
                using var valPaint = new SKPaint { Color = TextColor, TextSize = 9f, IsAntialias = true };
                float valW = valPaint.MeasureText(valStr);
                canvas.DrawText(valStr, W - valW - 3, ry + 13, valPaint);

                float barX  = labelEndX + 4;
                float barW  = W - barX - valW - 6;
                float barY  = ry + (rowH - barHt) / 2f;

                DrawBar(canvas, barCol, BarBg, barX, barY, barW, barHt, p.Value, p.Min, p.Max);
            }
            return Encode(surface);
        }

        // ── Single Tune Dial render (200×100 LCD) ────────────────────────────
        public static string RenderSingleDial(string tuneName, bool tuneEnabled,
                                              string paramLabel, double value,
                                              double min, double max)
        {
            const int W2    = 400;
            const int H2    = 200;
            const int HdrH2 = 44;

            var info    = new SKImageInfo(W2, H2, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info);
            var canvas  = surface.Canvas;
            canvas.Clear(BgColor);

            // ── Header ────────────────────────────────────────────────────────
            using var hdrPaint = new SKPaint { Color = HeaderBg };
            canvas.DrawRect(0, 0, W2, HdrH2, hdrPaint);

            string shortName = tuneName.Length > 14 ? tuneName[..14] : tuneName;
            using var hdrFont = new SKPaint { Color = TitleColor(tuneEnabled), TextSize = 22f, IsAntialias = true };
            canvas.DrawText(shortName, 8, 30, hdrFont);

            // Point ON/OFF
            using var dotPaint = new SKPaint { Color = DotColor(tuneEnabled), IsAntialias = true, IsStroke = !tuneEnabled, StrokeWidth = 3f };
            canvas.DrawCircle(W2 - 24, HdrH2 / 2f, 10f, dotPaint);

            // ── Param label ───────────────────────────────────────────────────
            float bodyY = HdrH2 + 8;
            using var labelPaint = new SKPaint { Color = tuneEnabled ? OnColor : DimColor, TextSize = 22f, IsAntialias = true };
            float lw = labelPaint.MeasureText(paramLabel);
            canvas.DrawText(paramLabel, (W2 - lw) / 2f, bodyY + 28, labelPaint);

            // ── Barre ─────────────────────────────────────────────────────────
            const float barH2  = 20f;
            const float barMX2 = 16f;
            float barW2 = W2 - barMX2 * 2;
            float barY2 = bodyY + 44;
            DrawBar(canvas, tuneEnabled ? OffColor : DimColor, BarBg, barMX2, barY2, barW2, barH2, value, min, max);

            // ── Valeur ────────────────────────────────────────────────────────
            string valStr = FormatVal(value);
            using var valPaint = new SKPaint
            {
                Color    = tuneEnabled ? OffColor : DimColor,
                TextSize = 52f,
                IsAntialias = true,
                Typeface = SKTypeface.FromFamilyName(null, SKFontStyleWeight.Bold,
                           SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
            };
            float vw = valPaint.MeasureText(valStr);
            canvas.DrawText(valStr, (W2 - vw) / 2f, barY2 + barH2 + 60, valPaint);

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
            using var hdrFont = new SKPaint { Color = titleCol, TextSize = 9f, IsAntialias = true };
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

                using var labelPaint = new SKPaint { Color = labelCol, TextSize = 7.5f, IsAntialias = true };
                canvas.DrawText(p.Label, 2, ry + 10, labelPaint);

                string valStr = FormatVal(p.Value);
                using var valPaint = new SKPaint { Color = TextColor, TextSize = 7.5f, IsAntialias = true };
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
            // Titre : +1 pixel vers le haut (12→11), +1 espace à gauche (2→5)
            using var hdrFont = new SKPaint { Color = TitleColor(tuneEnabled), TextSize = 9f, IsAntialias = true };
            canvas.DrawText(shortName, 5, 12, hdrFont);

            using var dotPaint = new SKPaint { Color = DotColor(tuneEnabled), IsAntialias = true, IsStroke = !tuneEnabled, StrokeWidth = 1.5f };
            canvas.DrawCircle(SW - 6, 9, 4, dotPaint);

            // ── Zone param ────────────────────────────────────────────────────
            float bodyY = SHdrH + 2;
            SKColor accent = tuneEnabled ? OffColor : new SKColor(80, 80, 80);

            // Label param : +2 pixels vers le haut, police plus grande (8→10)
            using var labelPaint = new SKPaint { Color = tuneEnabled ? OnColor : DimColor, TextSize = 10f, IsAntialias = true };
            float lw = labelPaint.MeasureText(paramLabel);
            canvas.DrawText(paramLabel, (SW - lw) / 2f, bodyY + 10, labelPaint);

            // Barre : +2px épaisseur (8→10), descend de 2px vers le bas
            const float barH  = 10f;
            const float barMX = 4f;
            float barW  = SW - barMX * 2;
            float barY  = bodyY + 20; // +2 vers le bas

            DrawBar(canvas, accent, BarBg, barMX, barY, barW, barH, value, min, max);

            // Valeur : descend de 2px (barH+18 → barH+20)
            string valStr = FormatVal(value);
            using var valPaint = new SKPaint { Color = tuneEnabled ? OffColor : DimColor, TextSize = 16f, IsAntialias = true };
            float vw = valPaint.MeasureText(valStr);
            canvas.DrawText(valStr, (SW - vw) / 2f, barY + barH + 20, valPaint);

            return Encode(surface);
        }

        // ── Profile button render (72x72) — style rouleau ────────────────────
        // prev   = profil précédent (petite ligne haut, fond bleu, texte orange)
        // active = profil actif     (ligne épaisse milieu, fond orange, texte bleu)
        // next   = profil suivant   (petite ligne bas, fond bleu, texte orange)
        public static string RenderProfileButton(string prevName, string activeName, string nextName, bool connected)
        {
            const int   SW      = 72;
            const int   SH      = 72;
            const float border  = 3.2f;
            const float sepH    = 6f;
            // Hauteurs bandes bleues +10% : 15.4px | orange réduit
            // total intérieur = 72 - 2*border = 65.6
            // blueH + sepH + orangeH + sepH + blueH = 65.6
            // → 15.4 + 6 + orangeH + 6 + 15.4 = 65.6 → orangeH = 22.8
            const float blueH   = 15.4f;
            const float orangeH = 22.8f;

            var info    = new SKImageInfo(SW, SH, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info);
            var canvas  = surface.Canvas;
            canvas.Clear(BgColor);

            if (!connected)
            {
                using var dimPaint = new SKPaint { Color = DimColor, TextSize = 8f, IsAntialias = true };
                canvas.DrawText("No device", 4, SH / 2f + 4, dimPaint);
                return Encode(surface);
            }

            float bx  = border;
            float bw  = SW - border * 2;           // inner width = 65.6
            float blueW = bw * 0.85f;              // 85% largeur pour bleu

            // ── Bande haut : prev — bleu 85% + noir 15% ──────────────────────
            float y0 = border;
            using var bluePaint   = new SKPaint { Color = OnColor };
            using var blackPaint  = new SKPaint { Color = new SKColor(17, 17, 17) };
            canvas.DrawRect(bx, y0, blueW, blueH, bluePaint);
            canvas.DrawRect(bx + blueW, y0, bw - blueW, blueH, blackPaint);

            string prev = prevName.Length > 10 ? prevName[..10] : prevName;
            using var prevTxt = new SKPaint { Color = new SKColor(17,17,17), TextSize = 7.5f, IsAntialias = true };
            canvas.DrawText(prev, bx + 3, y0 + blueH * 0.78f, prevTxt);

            // ── Séparateur haut ───────────────────────────────────────────────
            float y1 = y0 + blueH;
            canvas.DrawRect(bx, y1, bw, sepH, blackPaint);

            // ── Bande milieu : actif — orange pleine largeur ──────────────────
            float y2 = y1 + sepH;
            using var orangePaint = new SKPaint { Color = OffColor };
            canvas.DrawRect(bx, y2, bw, orangeH, orangePaint);

            string act = activeName.Length > 10 ? activeName[..10] : activeName;
            using var actTxt = new SKPaint { Color = new SKColor(17,17,17), TextSize = 11f, IsAntialias = true };
            // Réduire si trop long
            while (actTxt.MeasureText(act) > bw - 6 && act.Length > 1) act = act[..^1];
            float aw = actTxt.MeasureText(act);
            canvas.DrawText(act, bx + (bw - aw) / 2f, y2 + orangeH * 0.68f, actTxt);

            // ── Séparateur bas ────────────────────────────────────────────────
            float y3 = y2 + orangeH;
            canvas.DrawRect(bx, y3, bw, sepH, blackPaint);

            // ── Bande bas : next — bleu 85% + noir 15% ───────────────────────
            float y4 = y3 + sepH;
            canvas.DrawRect(bx, y4, blueW, blueH, bluePaint);
            canvas.DrawRect(bx + blueW, y4, bw - blueW, blueH, blackPaint);

            string nxt = nextName.Length > 10 ? nextName[..10] : nextName;
            using var nxtTxt = new SKPaint { Color = new SKColor(17,17,17), TextSize = 7.5f, IsAntialias = true };
            canvas.DrawText(nxt, bx + 3, y4 + blueH * 0.78f, nxtTxt);

            // ── Cadre extérieur noir ──────────────────────────────────────────
            using var framePaint = new SKPaint { Color = new SKColor(17,17,17), IsStroke = true, StrokeWidth = border * 2 };
            canvas.DrawRect(0, 0, SW, SH, framePaint);

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
