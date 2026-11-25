using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace RhythmTyping
{
    internal sealed class HandGuide
    {
        internal const int Thumb = 8;

        internal static readonly string[] FingerNames =
        {
            "Левый мизинец", "Левый безымянный", "Левый средний", "Левый указательный",
            "Правый указательный", "Правый средний", "Правый безымянный", "Правый мизинец", "Большой палец"
        };

        private static readonly Color[] FingerColors =
        {
            (Color)ColorConverter.ConvertFromString("#F19BAE"), (Color)ColorConverter.ConvertFromString("#F1BB82"),
            (Color)ColorConverter.ConvertFromString("#E0DA88"), (Color)ColorConverter.ConvertFromString("#78DBCF"),
            (Color)ColorConverter.ConvertFromString("#8ACCFA"), (Color)ColorConverter.ConvertFromString("#A5B4F7"),
            (Color)ColorConverter.ConvertFromString("#CFACF3"), (Color)ColorConverter.ConvertFromString("#F1A7D5"),
            (Color)ColorConverter.ConvertFromString("#9CE7B0")
        };

        private static readonly Color Deck = Color.FromRgb(20, 43, 83);
        private static readonly Color DarkInk = Color.FromRgb(15, 25, 38);
        private static readonly Color PalmHighlight = Color.FromRgb(48, 81, 137);
        private static readonly Color PalmShadow = Color.FromRgb(25, 48, 91);
        internal static readonly Brush ErrorFill = Solid(Color.FromRgb(99, 49, 66));
        internal static readonly Brush ErrorInk = Solid(Color.FromRgb(255, 195, 202));
        internal static readonly Brush Ink = Solid(DarkInk);

        private static readonly Brush[] Bright = MakePalette(1);
        private static readonly Brush[] KeyTints = MakePalette(0.16);
        private static readonly Brush[] Quiet = MakePalette(0.36);
        private static readonly Brush[] RestTints = MakePalette(0.65);

        private static readonly string[] DigitShapes =
        {
            "M 16,62 C 13,57 8,49 5,39 C 3,33 5,28 10,27 C 15,26 18,29 19,35 L 25,55 L 26,66 Z",
            "M 23,54 L 18,22 C 17,16 19,12 24,11 C 29,10 33,13 34,19 L 39,52 L 38,61 Z",
            "M 34,52 L 35,14 C 35,7 38,4 43,4 C 48,4 51,8 51,14 L 50,53 L 48,62 Z",
            "M 46,57 L 55,19 C 56,13 60,10 65,11 C 71,12 73,16 71,23 L 65,57 L 62,67 Z",
            "M 58,65 L 74,47 C 78,42 84,42 87,45 C 91,49 90,54 86,59 L 70,79 L 63,84 Z"
        };

        private const string PalmShape = "M 17,60 C 18,54 22,51 30,50 C 39,48 48,49 55,51 C 63,52 67,57 69,64 C 72,72 72,79 67,86 C 62,92 60,93 60,101 L 31,101 C 30,94 27,92 23,87 C 18,80 15,70 17,60 Z";

        private static readonly Point[] TipCenters =
        {
            new Point(12,37), new Point(26,21), new Point(43,14), new Point(63,21), new Point(81,52)
        };

        private sealed class Digit
        {
            internal int Finger;
            internal Path Shape;
            internal TextBlock Label;
        }

        private readonly Digit[] digits = new Digit[10];

        internal HandGuide(Canvas left, Canvas right)
        {
            BuildHand(left, false);
            BuildHand(right, true);
        }

        internal static string HomeKeys(bool english)
        {
            return english ? "asdfjkl;" : "фываолдж";
        }

        internal static Brush BrightColor(int finger) { return Bright[finger]; }
        internal static Brush KeyTint(int finger) { return KeyTints[finger]; }
        internal static Brush RestTint(int finger) { return RestTints[finger]; }

        internal void Show(string homeKeys, int current, int shift, bool homePosition, bool error)
        {
            foreach (Digit digit in digits)
            {
                int finger = digit.Finger;
                bool active = finger == current || finger == shift;
                digit.Shape.Fill = active ? (error ? ErrorFill : Bright[finger]) : homePosition ? RestTints[finger] : Quiet[finger];
                digit.Shape.Stroke = active && error ? ErrorInk : active || homePosition ? Bright[finger] : RestTints[finger];
                digit.Shape.StrokeThickness = active ? 1.7 : 1;
                digit.Label.Text = finger == Thumb ? "␣" : char.ToUpperInvariant(homeKeys[finger]).ToString();
                digit.Label.Foreground = active && !error || homePosition ? Ink : Bright[finger];
                if (active && error) digit.Label.Foreground = ErrorInk;
            }
        }

        private void BuildHand(Canvas canvas, bool right)
        {
            const double width = 94;
            Transform orientation = right ? (Transform)new MatrixTransform(-1, 0, 0, 1, width, 0) : Transform.Identity;
            int offset = right ? 5 : 0;
            for (int local = 0; local < DigitShapes.Length; local++)
            {
                int finger = local == 4 ? Thumb : right ? 7 - local : local;
                var digit = new Digit { Finger = finger, Shape = Outline(DigitShapes[local], orientation) };
                digits[offset + local] = digit;
                canvas.Children.Add(digit.Shape);
            }

            Path palm = Outline(PalmShape, orientation);
            palm.Fill = new LinearGradientBrush(PalmHighlight, PalmShadow, new Point(0, 0), new Point(1, 1));
            palm.Stroke = Solid(Color.FromRgb(88, 119, 176));
            canvas.Children.Add(palm);

            for (int local = 0; local < DigitShapes.Length; local++)
            {
                Point center = TipCenters[local];
                var label = new TextBlock
                {
                    Width = 17, Height = 15, TextAlignment = TextAlignment.Center,
                    FontFamily = new FontFamily("Segoe UI"), FontWeight = FontWeights.Bold, FontSize = 10
                };
                Canvas.SetLeft(label, (right ? width - center.X : center.X) - label.Width / 2);
                Canvas.SetTop(label, center.Y - label.Height / 2);
                canvas.Children.Add(label);
                digits[offset + local].Label = label;
            }
        }

        private static Path Outline(string shape, Transform orientation)
        {
            Geometry geometry = Geometry.Parse(shape).Clone();
            geometry.Transform = orientation;
            geometry.Freeze();
            return new Path { Data = geometry, StrokeThickness = 1, StrokeLineJoin = PenLineJoin.Round };
        }

        private static Brush[] MakePalette(double strength)
        {
            var palette = new Brush[FingerColors.Length];
            for (int index = 0; index < palette.Length; index++)
            {
                Color finger = FingerColors[index];
                palette[index] = Solid(Color.FromRgb(
                    (byte)(Deck.R + (finger.R - Deck.R) * strength),
                    (byte)(Deck.G + (finger.G - Deck.G) * strength),
                    (byte)(Deck.B + (finger.B - Deck.B) * strength)));
            }
            return palette;
        }

        private static Brush Solid(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }
}
