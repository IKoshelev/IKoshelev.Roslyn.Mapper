using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IKoshelev.Roslyn.Mapper
{
    public static class SymbolExtensions
    {
        public static bool IsInheritingFrom(
                this INamedTypeSymbol typeInheriting,
                INamedTypeSymbol typeInherited)
        {
            if (typeInheriting == typeInherited)
            {
                return true;
            }

            var currentyType = typeInheriting.BaseType;

            while (currentyType != null)
            {
                if (currentyType == typeInherited)
                {
                    return true;
                }
                currentyType = currentyType.BaseType;
            }

            return false;
        }
    }
}
