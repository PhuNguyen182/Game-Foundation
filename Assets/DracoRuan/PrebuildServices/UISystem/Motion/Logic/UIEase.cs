using System;

namespace DracoRuan.PrebuildServices.UISystem.Motion.Logic
{
    /// <summary>
    /// Standard easing formulas (Penner/easings.net), normalized to t/value in [0,1]
    /// (Back/Elastic overshoot outside that range by design). Engine-agnostic: used by
    /// both runtime track evaluation and editor timeline preview without a UnityEngine
    /// dependency, and matches the numeric formulas DOTween/PrimeTween use.
    /// </summary>
    public static class UIEase
    {
        private const double Pi = Math.PI;
        private const double BackC1 = 1.70158;
        private const double BackC2 = BackC1 * 1.525;
        private const double BackC3 = BackC1 + 1.0;
        private const double ElasticC4 = 2.0 * Pi / 3.0;
        private const double ElasticC5 = 2.0 * Pi / 4.5;

        public static float Evaluate(UIEaseType type, float t)
        {
            double x = t;
            switch (type)
            {
                case UIEaseType.Linear: return (float)x;

                case UIEaseType.InSine: return (float)InSine(x);
                case UIEaseType.OutSine: return (float)OutSine(x);
                case UIEaseType.InOutSine: return (float)InOutSine(x);

                case UIEaseType.InQuad: return (float)Math.Pow(x, 2);
                case UIEaseType.OutQuad: return (float)(1.0 - Math.Pow(1.0 - x, 2));
                case UIEaseType.InOutQuad: return (float)InOutPow(x, 2);

                case UIEaseType.InCubic: return (float)Math.Pow(x, 3);
                case UIEaseType.OutCubic: return (float)(1.0 - Math.Pow(1.0 - x, 3));
                case UIEaseType.InOutCubic: return (float)InOutPow(x, 3);

                case UIEaseType.InQuart: return (float)Math.Pow(x, 4);
                case UIEaseType.OutQuart: return (float)(1.0 - Math.Pow(1.0 - x, 4));
                case UIEaseType.InOutQuart: return (float)InOutPow(x, 4);

                case UIEaseType.InQuint: return (float)Math.Pow(x, 5);
                case UIEaseType.OutQuint: return (float)(1.0 - Math.Pow(1.0 - x, 5));
                case UIEaseType.InOutQuint: return (float)InOutPow(x, 5);

                case UIEaseType.InExpo: return (float)InExpo(x);
                case UIEaseType.OutExpo: return (float)OutExpo(x);
                case UIEaseType.InOutExpo: return (float)InOutExpo(x);

                case UIEaseType.InCirc: return (float)(1.0 - Math.Sqrt(1.0 - Math.Pow(x, 2)));
                case UIEaseType.OutCirc: return (float)Math.Sqrt(1.0 - Math.Pow(x - 1.0, 2));
                case UIEaseType.InOutCirc: return (float)InOutCirc(x);

                case UIEaseType.InBack: return (float)(BackC3 * x * x * x - BackC1 * x * x);
                case UIEaseType.OutBack: return (float)OutBack(x);
                case UIEaseType.InOutBack: return (float)InOutBack(x);

                case UIEaseType.InElastic: return (float)InElastic(x);
                case UIEaseType.OutElastic: return (float)OutElastic(x);
                case UIEaseType.InOutElastic: return (float)InOutElastic(x);

                case UIEaseType.InBounce: return (float)(1.0 - OutBounce(1.0 - x));
                case UIEaseType.OutBounce: return (float)OutBounce(x);
                case UIEaseType.InOutBounce: return (float)InOutBounce(x);

                default: return (float)x;
            }
        }

        private static double InSine(double x) => 1.0 - Math.Cos(x * Pi / 2.0);
        private static double OutSine(double x) => Math.Sin(x * Pi / 2.0);
        private static double InOutSine(double x) => -(Math.Cos(Pi * x) - 1.0) / 2.0;

        private static double InOutPow(double x, int power) =>
            x < 0.5 ? Math.Pow(2.0 * x, power) / 2.0 : 1.0 - Math.Pow(-2.0 * x + 2.0, power) / 2.0;

        private static double InExpo(double x) => x <= 0.0 ? 0.0 : Math.Pow(2.0, 10.0 * x - 10.0);
        private static double OutExpo(double x) => x >= 1.0 ? 1.0 : 1.0 - Math.Pow(2.0, -10.0 * x);

        private static double InOutExpo(double x)
        {
            if (x <= 0.0) return 0.0;
            if (x >= 1.0) return 1.0;
            return x < 0.5 ? Math.Pow(2.0, 20.0 * x - 10.0) / 2.0 : (2.0 - Math.Pow(2.0, -20.0 * x + 10.0)) / 2.0;
        }

        private static double InOutCirc(double x) =>
            x < 0.5
                ? (1.0 - Math.Sqrt(1.0 - Math.Pow(2.0 * x, 2))) / 2.0
                : (Math.Sqrt(1.0 - Math.Pow(-2.0 * x + 2.0, 2)) + 1.0) / 2.0;

        private static double OutBack(double x) =>
            1.0 + BackC3 * Math.Pow(x - 1.0, 3) + BackC1 * Math.Pow(x - 1.0, 2);

        private static double InOutBack(double x)
        {
            return x < 0.5
                ? Math.Pow(2.0 * x, 2) * ((BackC2 + 1.0) * 2.0 * x - BackC2) / 2.0
                : (Math.Pow(2.0 * x - 2.0, 2) * ((BackC2 + 1.0) * (x * 2.0 - 2.0) + BackC2) + 2.0) / 2.0;
        }

        private static double InElastic(double x)
        {
            if (x <= 0.0) return 0.0;
            if (x >= 1.0) return 1.0;
            return -Math.Pow(2.0, 10.0 * x - 10.0) * Math.Sin((x * 10.0 - 10.75) * ElasticC4);
        }

        private static double OutElastic(double x)
        {
            if (x <= 0.0) return 0.0;
            if (x >= 1.0) return 1.0;
            return Math.Pow(2.0, -10.0 * x) * Math.Sin((x * 10.0 - 0.75) * ElasticC4) + 1.0;
        }

        private static double InOutElastic(double x)
        {
            if (x <= 0.0) return 0.0;
            if (x >= 1.0) return 1.0;
            return x < 0.5
                ? -(Math.Pow(2.0, 20.0 * x - 10.0) * Math.Sin((20.0 * x - 11.125) * ElasticC5)) / 2.0
                : Math.Pow(2.0, -20.0 * x + 10.0) * Math.Sin((20.0 * x - 11.125) * ElasticC5) / 2.0 + 1.0;
        }

        private static double OutBounce(double x)
        {
            const double n1 = 7.5625;
            const double d1 = 2.75;

            if (x < 1.0 / d1)
            {
                return n1 * x * x;
            }

            if (x < 2.0 / d1)
            {
                x -= 1.5 / d1;
                return n1 * x * x + 0.75;
            }

            if (x < 2.5 / d1)
            {
                x -= 2.25 / d1;
                return n1 * x * x + 0.9375;
            }

            x -= 2.625 / d1;
            return n1 * x * x + 0.984375;
        }

        private static double InOutBounce(double x) =>
            x < 0.5
                ? (1.0 - OutBounce(1.0 - 2.0 * x)) / 2.0
                : (1.0 + OutBounce(2.0 * x - 1.0)) / 2.0;
    }
}
