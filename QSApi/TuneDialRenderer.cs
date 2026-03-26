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

        private static SKColor BgColor      = new SKColor(18,  18,  18);
        private static SKColor HeaderBg     = new SKColor(30,  30,  30);
        private static SKColor BarBg        = new SKColor(50,  50,  50);
        private static SKColor OnColor      = new SKColor(0x5D,0xB3,0xE2); // blue
        private static SKColor OffColor     = new SKColor(0xFD,0x89,0x0D); // orange
        private static SKColor TextColor    = new SKColor(180, 180, 180);
        private static SKColor HeaderColor  = new SKColor(255, 200, 80);
        private static SKColor SepColor     = new SKColor(45,  45,  45);

        public record ParamDisplay(string Label, double Value, double Min, double Max, bool Enabled, bool Selected = false);

        public static string Render(string tuneName, bool tuneEnabled, List<ParamDisplay> parameters)
        {
            var info    = new SKImageInfo(W, H, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info);
            var canvas  = surface.Canvas;
            canvas.Clear(BgColor);

            // ── Header ────────────────────────────────────────────────────────
            using var hdrPaint = new SKPaint { Color = HeaderBg };
            canvas.DrawRect(0, 0, W, HeaderH, hdrPaint);

            using var hdrFont = new SKPaint
            {
                Color       = HeaderColor,
                TextSize    = 11f,
                IsAntialias = true,
                FakeBoldText= true
            };
            canvas.DrawText(tuneName, 4, 15, hdrFont);

            // ON/OFF top right — color only, no checkbox
            string stateStr  = tuneEnabled ? "● ON" : "○ OFF";
            SKColor stateCol = tuneEnabled ? OnColor : OffColor;
            using var statePaint = new SKPaint
            {
                Color       = stateCol,
                TextSize    = 9f,
                IsAntialias = true,
                FakeBoldText= true
            };
            float stateW = statePaint.MeasureText(stateStr);
            canvas.DrawText(stateStr, W - stateW - 4, 15, statePaint);

            // ── Param rows ────────────────────────────────────────────────────
            int count = Math.Min(parameters.Count, 3);
            if (count == 0) return Encode(surface);

            float rowH  = (H - HeaderH) / (float)count;
            float barHt = 10f; // 2x taller bar

            using var sepPaint   = new SKPaint { Color = SepColor };
            using var barBgPaint = new SKPaint { Color = BarBg };

            // Measure max label width for vertical alignment
            using var measurePaint = new SKPaint { TextSize = 9f, IsAntialias = true };
            float maxLabelW = 0;
            foreach (var p in parameters)
            {
                float w = measurePaint.MeasureText(p.Label);
                if (w > maxLabelW) maxLabelW = w;
            }
            float labelEndX = maxLabelW + 6; // all labels aligned to this X

            for (int i = 0; i < count; i++)
            {
                var   p   = parameters[i];
                float ry  = HeaderH + i * rowH;

                // Row separator
                canvas.DrawLine(0, ry, W, ry, sepPaint);

                // Colors: selected = orange, others = blue
                SKColor labelCol = p.Selected ? OffColor : OnColor;
                SKColor barCol   = p.Selected ? OffColor : OnColor;

                // Override if tune is OFF — everything orange/dim
                if (!tuneEnabled)
                {
                    labelCol = p.Selected ? OffColor : new SKColor(120, 120, 120);
                    barCol   = p.Selected ? OffColor : new SKColor(80,  80,  80);
                }

                // Label — right-aligned to labelEndX
                using var labelPaint = new SKPaint
                {
                    Color       = labelCol,
                    TextSize    = 9f,
                    IsAntialias = true,
                    FakeBoldText= p.Selected
                };
                float lw = labelPaint.MeasureText(p.Label);
                canvas.DrawText(p.Label, labelEndX - lw, ry + 13, labelPaint);

                // Value — right side
                string valStr = FormatVal(p.Value);
                using var valPaint = new SKPaint
                {
                    Color       = TextColor,
                    TextSize    = 9f,
                    IsAntialias = true,
                    FakeBoldText= true
                };
                float valW = valPaint.MeasureText(valStr);
                canvas.DrawText(valStr, W - valW - 3, ry + 13, valPaint);

                // Bar — between label and value
                float barX  = labelEndX + 4;
                float barW  = W - barX - valW - 6;
                float barY  = ry + (rowH - barHt) / 2f;

                // Bar background
                canvas.DrawRoundRect(barX, barY, barW, barHt, 2, 2, barBgPaint);

                // Bar fill
                double range = p.Max - p.Min;
                double norm  = range > 0 ? Math.Clamp((p.Value - p.Min) / range, 0, 1) : 0;
                float  fillW = (float)(barW * norm);

                if (fillW > 0)
                {
                    using var fillPaint = new SKPaint { Color = barCol, IsAntialias = true };
                    canvas.DrawRoundRect(barX, barY, fillW, barHt, 2, 2, fillPaint);
                }
            }

            return Encode(surface);
        }

        // ── Status button render (72x72 keypad) ───────────────────────────────
        public static string RenderStatus(string tuneName, bool tuneEnabled, List<ParamDisplay> parameters)
        {
            const int SW      = 72;
            const int SH      = 72;
            const int SHdrH   = 18;
            const float SBarH = 4f;

            var info    = new SKImageInfo(SW, SH, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info);
            var canvas  = surface.Canvas;
            canvas.Clear(BgColor);

            // Header
            using var hdrPaint = new SKPaint { Color = HeaderBg };
            canvas.DrawRect(0, 0, SW, SHdrH, hdrPaint);

            string shortName = tuneName.Length > 9 ? tuneName[..9] : tuneName;
            using var hdrFont = new SKPaint { Color = HeaderColor, TextSize = 9f, IsAntialias = true, FakeBoldText = true };
            canvas.DrawText(shortName, 2, 13, hdrFont);

            // ON/OFF dot
            SKColor dotCol = tuneEnabled ? OnColor : OffColor;
            using var dotPaint = new SKPaint { Color = dotCol, IsAntialias = true };
            canvas.DrawCircle(SW - 6, 9, 4, dotPaint);

            // Param rows
            int count = Math.Min(parameters.Count, 3);
            if (count == 0) return Encode(surface);
            float rowH = (SH - SHdrH) / (float)count;

            using var sepPaint   = new SKPaint { Color = SepColor };
            using var barBgPaint = new SKPaint { Color = BarBg };

            for (int i = 0; i < count; i++)
            {
                var   p   = parameters[i];
                float ry  = SHdrH + i * rowH;
                bool  sel = p.Selected;

                canvas.DrawLine(0, ry, SW, ry, sepPaint);

                SKColor barCol   = tuneEnabled ? (sel ? OffColor : OnColor) : new SKColor(80, 80, 80);
                SKColor labelCol = tuneEnabled ? (sel ? OffColor : OnColor) : new SKColor(120, 120, 120);

                using var labelPaint = new SKPaint { Color = labelCol, TextSize = 7.5f, IsAntialias = true, FakeBoldText = sel };
                canvas.DrawText(p.Label, 2, ry + 10, labelPaint);

                string valStr = FormatVal(p.Value);
                using var valPaint = new SKPaint { Color = TextColor, TextSize = 7.5f, IsAntialias = true, FakeBoldText = true };
                float valW = valPaint.MeasureText(valStr);
                canvas.DrawText(valStr, SW - valW - 2, ry + 10, valPaint);

                float barY = ry + rowH - SBarH - 1;
                canvas.DrawRect(0, barY, SW, SBarH, barBgPaint);
                double range = p.Max - p.Min;
                double norm  = range > 0 ? Math.Clamp((p.Value - p.Min) / range, 0, 1) : 0;
                float  fillW = (float)(SW * norm);
                if (fillW > 0)
                {
                    using var fillPaint = new SKPaint { Color = barCol };
                    canvas.DrawRect(0, barY, fillW, SBarH, fillPaint);
                }
            }

            return Encode(surface);
        }

        private static string Encode(SKSurface surface)
        {
            using var img  = surface.Snapshot();
            using var data = img.Encode(SKEncodedImageFormat.Png, 100);
            return "data:image/png;base64," + Convert.ToBase64String(data.ToArray());
        }

        private static string FormatVal(double v)
        {
            if (Math.Abs(v) >= 100) return v.ToString("F0");
            if (Math.Abs(v) >= 10)  return v.ToString("F1");
            return v.ToString("F2");
        }
    }
}
