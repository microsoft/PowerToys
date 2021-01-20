namespace Mages.Core.Tests.Mocks
{
    using Mages.Core.Runtime.Converters;
    using System;
    using System.Collections.Generic;
    using System.Reflection;

    sealed class NameSelectorMock : INameSelector
    {
        private readonly Func<MemberInfo, String> _resolve;

        public NameSelectorMock(Func<MemberInfo, String> resolve)
        {
            _resolve = resolve;
        }

        public String Select(IEnumerable<String> registered, MemberInfo member)
        {
            return _resolve.Invoke(member);
        }
    }
}
