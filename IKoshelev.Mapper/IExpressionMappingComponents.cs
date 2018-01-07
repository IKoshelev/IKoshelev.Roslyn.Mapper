using System;
using System.Linq.Expressions;
using IKoshelev.Mapper.ExpressionCombiner;

namespace IKoshelev.Mapper
{
    public interface IMappingComponents<TSource, TDestination> where TDestination : new()
    {
        IgnoreList<TSource> SourceIgnoredProperties { get; }
        IgnoreList<TDestination> TargetIgnoredProperties { get; }
        Expression<Func<TSource, TDestination>> DefaultMappings { get; }
        Expression<Func<TSource, TDestination>> CustomMappings { get; }
        Expression<Func<TSource, TDestination>> CombinedMappingsWithConstructor { get; }
        Expression<Action<TSource, TDestination>> CombinedMappingsForExistingTarget { get; }
    }

    public class ExpressionMappingComponents<TSource, TDestination> : IMappingComponents<TSource, TDestination> where TDestination : new()
    {
        public ExpressionMappingComponents()
        {
        }

        public ExpressionMappingComponents(
            Expression<Func<TSource, TDestination>> defaultMappings,
            Expression<Func<TSource, TDestination>> customMappings = null,
            IgnoreList<TSource> sourceIgnoredProperties = null,
            IgnoreList<TDestination> targetIgnoredProperties = null)
        {
            DefaultMappings = defaultMappings ?? throw new ArgumentNullException(nameof(defaultMappings));
            CustomMappings = customMappings;
            SourceIgnoredProperties = sourceIgnoredProperties ?? new IgnoreList<TSource>();
            TargetIgnoredProperties = targetIgnoredProperties ?? new IgnoreList<TDestination>();
        }

        public IgnoreList<TSource> SourceIgnoredProperties { get; private set; }
        public IgnoreList<TDestination> TargetIgnoredProperties { get; private set; }
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
                    return defaultMappings;
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
                var customMappings = CustomMappings ?? ((source) => new TDestination() { });

                var combiner = new ExpressionCombiner<TSource, TDestination>();

                var combined = combiner.CombineIntoMapperForExisting(customMappings, defaultMappings);

                return combined;
            }
        }
    }
}
