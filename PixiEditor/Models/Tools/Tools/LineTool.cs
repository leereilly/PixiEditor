﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PixiEditor.Helpers.Extensions;
using PixiEditor.Models.DataHolders;
using PixiEditor.Models.Enums;
using PixiEditor.Models.Layers;
using PixiEditor.Models.Position;
using PixiEditor.Models.Tools.ToolSettings.Settings;
using PixiEditor.Models.Tools.ToolSettings.Toolbars;
using SkiaSharp;

namespace PixiEditor.Models.Tools.Tools
{
    public class LineTool : ShapeTool
    {
        private readonly CircleTool circleTool;
        private List<Coordinates> linePoints = new List<Coordinates>();

        public LineTool()
        {
            ActionDisplay = "Click and move to draw a line. Hold Shift to draw an even one.";
            Toolbar = new BasicToolbar();
            circleTool = new CircleTool();
        }

        public override string Tooltip => "Draws line on canvas (L). Hold Shift to draw even line.";

        public override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.LeftShift)
            {
                ActionDisplay = "Click and move mouse to draw an even line.";
            }
        }

        public override void OnKeyUp(KeyEventArgs e)
        {
            if (e.Key == Key.LeftShift)
            {
                ActionDisplay = "Click and move to draw a line. Hold Shift to draw an even one.";
            }
        }

        public override void Use(Layer layer, List<Coordinates> coordinates, SKColor color)
        {
            int thickness = Toolbar.GetSetting<SizeSetting>("ToolSize").Value;

            DoubleCords fixedCoordinates = CalculateCoordinatesForShapeRotation(coordinates[^1], coordinates[0]);

            int halfThickness = (int)Math.Ceiling(thickness / 2.0);
            Int32Rect dirtyRect = new Int32Rect(
                fixedCoordinates.Coords1.X - halfThickness,
                fixedCoordinates.Coords1.Y - halfThickness,
                fixedCoordinates.Coords2.X + (halfThickness * 2) - fixedCoordinates.Coords1.X,
                fixedCoordinates.Coords2.Y + (halfThickness * 2) - fixedCoordinates.Coords1.Y);
            Int32Rect curLayerRect = new(layer.OffsetX, layer.OffsetY, layer.Width, layer.Height);
            Int32Rect expanded = dirtyRect.Expand(curLayerRect);

            layer.DynamicResize(expanded.X + expanded.Width - 1, expanded.Y + expanded.Height - 1, expanded.X, expanded.Y);

            using (SKPaint paint = new SKPaint())
            {
                int x = fixedCoordinates.Coords1.X;
                int y = fixedCoordinates.Coords1.Y;
                int x1 = fixedCoordinates.Coords2.X;
                int y1 = fixedCoordinates.Coords2.Y;


                paint.StrokeWidth = thickness;
                paint.Style = SKPaintStyle.Stroke;
                paint.Color = color;
                layer.LayerBitmap.SkiaSurface.Canvas.DrawLine(x, y, x1, y1, paint);
            }
            layer.InvokeLayerBitmapChange(dirtyRect);
        }

        public List<Coordinates> CreateLine(Layer layer, SKColor color, Coordinates start, Coordinates end, int thickness)
        {
            return CreateLineFastest(layer, color, start, end, thickness);
        }

        private List<Coordinates> CreateLine(Layer layer, SKColor color, IEnumerable<Coordinates> coordinates, int thickness, CapType startCap, CapType endCap)
        {
            Coordinates startingCoordinates = coordinates.Last();
            Coordinates latestCoordinates = coordinates.First();

            return CreateLine(layer, color, startingCoordinates, latestCoordinates, thickness, startCap, endCap);
        }

        private List<Coordinates> CreateLine(Layer layer, SKColor color, Coordinates start, Coordinates end, int thickness, CapType startCap, CapType endCap)
        {
            if (thickness == 1)
            {
                return BresenhamLine(layer, color, start.X, start.Y, end.X, end.Y);
            }

            return GenerateLine(layer, color, start, end, thickness, startCap, endCap);
        }

        private List<Coordinates> CreateLineFastest(Layer layer, SKColor color, Coordinates start, Coordinates end, int thickness)
        {
            var line = BresenhamLine(layer, color, start.X, start.Y, end.X, end.Y);
            if (thickness == 1)
            {
                return line;
            }

            ThickenShape(layer, color, line, thickness);
            return line;
        }

        private List<Coordinates> GenerateLine(Layer layer, SKColor color, Coordinates start, Coordinates end, int thickness, CapType startCap, CapType endCap)
        {
            ApplyCap(layer, color, startCap, start, thickness);
            if (start == end)
            {
                return new List<Coordinates>() { start };
            }

            var line = BresenhamLine(layer, color, start.X, start.Y, end.X, end.Y);

            ApplyCap(layer, color, endCap, end, thickness);
            if (line.Count() > 2)
            {
                ThickenShape(layer, color, line.Except(new[] { start, end }), thickness);
            }

            return line;
        }

        private void ApplyCap(Layer layer, SKColor color, CapType cap, Coordinates position, int thickness)
        {
            switch (cap)
            {
                case CapType.Round:
                    ApplyRoundCap(layer, color, position, thickness); // Round cap is not working very well, circle tool must be improved
                    break;

                default:
                    ThickenShape(layer, color, position, thickness);
                    break;
            }
        }

        /// <summary>
        ///     Gets points for rounded cap on specified position and thickness.
        /// </summary>
        /// <param name="position">Starting position of cap.</param>
        /// <param name="thickness">Thickness of cap.</param>
        private void ApplyRoundCap(Layer layer, SKColor color, Coordinates position, int thickness)
        {
            IEnumerable<Coordinates> rectangleCords = CoordinatesCalculator.RectangleToCoordinates(
                CoordinatesCalculator.CalculateThicknessCenter(position, thickness));
            circleTool.CreateEllipse(layer, color, rectangleCords.First(), rectangleCords.Last(), 1, true);
        }

        private List<Coordinates> BresenhamLine(Layer layer, SKColor color, int x1, int y1, int x2, int y2)
        {
            //using BitmapContext context = layer.LayerBitmap.GetBitmapContext();
            linePoints.Clear();
            Coordinates cords;
            if (x1 == x2 && y1 == y2)
            {
                cords = new Coordinates(x1, y1);
                layer.SetPixelWithOffset(cords, color);
                linePoints.Add(cords);
            }

            int d, dx, dy, ai, bi, xi, yi;
            int x = x1, y = y1;

            if (x1 < x2)
            {
                xi = 1;
                dx = x2 - x1;
            }
            else
            {
                xi = -1;
                dx = x1 - x2;
            }

            if (y1 < y2)
            {
                yi = 1;
                dy = y2 - y1;
            }
            else
            {
                yi = -1;
                dy = y1 - y2;
            }

            cords = new Coordinates(x, y);
            layer.SetPixelWithOffset(cords, color);
            linePoints.Add(cords);

            if (dx > dy)
            {
                ai = (dy - dx) * 2;
                bi = dy * 2;
                d = bi - dx;

                while (x != x2)
                {
                    if (d >= 0)
                    {
                        x += xi;
                        y += yi;
                        d += ai;
                    }
                    else
                    {
                        d += bi;
                        x += xi;
                    }

                    cords = new Coordinates(x, y);
                    layer.SetPixelWithOffset(cords, color);
                    linePoints.Add(cords);
                }
            }
            else
            {
                ai = (dx - dy) * 2;
                bi = dx * 2;
                d = bi - dy;

                while (y != y2)
                {
                    if (d >= 0)
                    {
                        x += xi;
                        y += yi;
                        d += ai;
                    }
                    else
                    {
                        d += bi;
                        y += yi;
                    }

                    cords = new Coordinates(x, y);
                    layer.SetPixelWithOffset(cords, color);
                    linePoints.Add(cords);
                }
            }

            return linePoints;
        }
    }
}
