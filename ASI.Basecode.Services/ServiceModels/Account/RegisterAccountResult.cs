using System;
using System.Collections.Generic;

namespace ASI.Basecode.Services.ServiceModels.Account
{
    public sealed class RegisterAccountResult
    {
        public bool Succeeded { get; private set; }
        public IReadOnlyList<string> Errors { get; private set; } =
            new string[0];

        public static RegisterAccountResult Success() =>
            new RegisterAccountResult { Succeeded = true };

        public static RegisterAccountResult Failure(
            IEnumerable<string> errors) =>
            new RegisterAccountResult
            {
                Succeeded = false,
                Errors = new List<string>(errors)
            };
    }
}
