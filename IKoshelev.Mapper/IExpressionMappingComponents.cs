using System;
using System.Linq.Expressions;
using IKoshelev.Mapper.ExpressionCombiner;

namespace IKoshelev.Mapper
{
    public interface IMappingComponents<TSource, TDestination> where TDestination: new()
    {
        Expression<Func<TSource, object>>[] SourceIgnoredProperties { get; }
        Expression<Func<TDestination, object>>[] TargetIgnoredProperties { get; }
        Expression<Func<TSource, TDestination>> DefaultMappings { get; }
        Expression<Func<TSource, TDestination>> CustomMappings { get; }
        Expression<Func<TSource, TDestination>> CombinedMappingsWithConstructor { get; }
        Expression<Action<TSource, TDestination>> CombinedMappingsForExistingTarget { get; }
    }

    public class ExpressionMappingComponents<TSource, TDestination> : IMappingComponents<TSource, TDestination> where TDestination : new()
    {
        public ExpressionMappingComponents(
            Expression<Func<TSource, TDestination>> defaultMappings,
            Expression<Func<TSource, TDestination>> customMappings = null,
            Expression<Func<TSource, object>>[] sourceIgnoredProperties = null,
            Expression<Func<TDestination, object>>[] targetIgnoredProperties = null)
        {
            DefaultMappings = defaultMappings ?? throw new ArgumentNullException(nameof(defaultMappings));
            CustomMappings = customMappings;
            SourceIgnoredProperties = sourceIgnoredProperties ?? new Expression<Func<TSource, object>>[0];
            TargetIgnoredProperties = targetIgnoredProperties ?? new Expression<Func<TDestination, object>>[0];
        }

        public Expression<Func<TSource, object>>[] SourceIgnoredProperties { get; private set; }
        public Expression<Func<TDestination, object>>[] TargetIgnoredProperties { get; private set; }
        public Expression<Func<TSource, TDestination>> DefaultMappings { get; private set; }
        public Expression<Func<TSource, TDestination>> CustomMappings { get; private set; }

        public Expression<Func<TSource, TDestination>> CombinedMappingsWithConstructor
        {
            get
            {
                var defaultMappings = DefaultMappings;
                var customMappings = CustomMappings;

                if (customMappings == null)
                {
                    return customMappings;
                }

                var combiner = new ExpressionCombiner<TSource, TDestination>();

                var combined = combiner.CombineIntoMapperWithConstructor(customMappings, defaultMappings);

                return combined;
            }
        }

        public Expression<Action<TSource, TDestination>> CombinedMappingsForExistingTarget
        {
            get
            {
                var defaultMappings = DefaultMappings;
                var customMappings = CustomMappings ?? ((source) => new TDestination());

                var combiner = new ExpressionCombiner<TSource, TDestination>();

                var combined = combiner.CombineIntoMapperForExisting(customMappings, defaultMappings);

                return combined;
            }
        }
    }
}
