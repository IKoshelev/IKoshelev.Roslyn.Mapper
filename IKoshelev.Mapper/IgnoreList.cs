using System;
using System.Linq.Expressions;

namespace IKoshelev.Mapper
{
    public class IgnoreList<T>
    {
        public Expression<Func<T, object>>[] IgnoredMembers { get; set; }
        public IgnoreList(params Expression<Func<T, object>>[] ignoredMembers)
        {
            IgnoredMembers = ignoredMembers;
        }        
    }   
}
