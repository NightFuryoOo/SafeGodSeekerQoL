
namespace GodhomeQoL.Settings
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class GlobalSettingAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class LocalSettingAttribute : Attribute
    {
    }

    public enum OptionType
    {
        Option,
        Slider,
    }

    [AttributeUsage(AttributeTargets.Field)]
    internal abstract class OptionAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Field)]
    internal sealed class BoolOptionAttribute : OptionAttribute
    {
    }

    [AttributeUsage(AttributeTargets.Field)]
    internal sealed class IntOptionAttribute : OptionAttribute
    {
        internal int[] Options { get; private init; }
        internal OptionType Type { get; private init; } = OptionType.Option;

        internal IntOptionAttribute(int start, int stop, int step = 1)
        {
            List<int> options = [];

            for (int i = start; i < stop; i += step)
            {
                options.Add(i);
            }

            options.Add(stop);
            Options = [.. options];
        }

        internal IntOptionAttribute(int start, int stop, OptionType type)
        {
            Options = [start, stop];
            Type = type;
        }

        internal IntOptionAttribute(params int[] options) => Options = options;
    }

    [AttributeUsage(AttributeTargets.Field)]
    internal sealed class FloatOptionAttribute : OptionAttribute
    {
        internal float[] Options { get; private init; }
        internal OptionType Type { get; private init; } = OptionType.Option;

        internal FloatOptionAttribute(float start, float stop, float step)
        {
            List<float> options = [];

            decimal decimalStop = (decimal)stop, decimalStep = (decimal)step;
            for (decimal i = (decimal)start; i < decimalStop; i += decimalStep)
            {
                options.Add(decimal.ToSingle(i));
            }

            options.Add(stop);
            Options = [.. options];
        }

        internal FloatOptionAttribute(float start, float stop, OptionType type)
        {
            Options = [start, stop];
            Type = type;
        }

        internal FloatOptionAttribute(params float[] options) => Options = options;
    }

    [AttributeUsage(AttributeTargets.Field)]
    internal sealed class EnumOptionAttribute : OptionAttribute
    {
    }

    [AttributeUsage(AttributeTargets.Field)]
    internal sealed class ReloadOnUpdateAttribute : Attribute
    {
    }
}
