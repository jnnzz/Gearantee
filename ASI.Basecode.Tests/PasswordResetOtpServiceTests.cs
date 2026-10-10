using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Services;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ASI.Basecode.Tests
{
    public sealed class PasswordResetOtpServiceTests
    {
        [Fact]
        public void StateRoundTripsWithAttemptAndIssuanceMetadata()
        {
            var now = DateTimeOffset.FromUnixTimeSeconds(
                DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            var state = new PasswordResetOtpState(
                now.AddMinutes(10),
                2,
                now.AddMinutes(-4),
                3,
                now.AddMinutes(-1),
                "hashed-otp");

            var parsed = PasswordResetOtpState.TryParse(
                state.Serialize(),
                out var restored);

            Assert.True(parsed);
            Assert.Equal(state.ExpiresAt, restored.ExpiresAt);
            Assert.Equal(state.Attempts, restored.Attempts);
            Assert.Equal(
                state.IssuanceWindowStartAt,
                restored.IssuanceWindowStartAt);
            Assert.Equal(state.IssuanceCount, restored.IssuanceCount);
            Assert.Equal(state.LastIssuedAt, restored.LastIssuedAt);
            Assert.Equal(state.OtpHash, restored.OtpHash);
        }

        [Fact]
        public async Task ConcurrentReservationsNeverCheckMoreThanFiveGuesses()
        {
            var now = DateTimeOffset.UtcNow;
            var initialState = new PasswordResetOtpState(
                now.AddMinutes(10),
                0,
                now,
                1,
                now,
                "hashed-otp");
            var store = new FakeOtpStore(initialState.Serialize());
            var service = new PasswordResetOtpService(store);
            var checkedGuessCount = 0;

            var requests = Enumerable.Range(0, 100)
                .Select(async _ =>
                {
                    var reservation = await service.TryReserveAttemptAsync(
                        "user-1",
                        now);
                    if (!reservation.IsReserved)
                    {
                        return;
                    }

                    Interlocked.Increment(ref checkedGuessCount);
                    if (reservation.State.Attempts >=
                        PasswordResetOtpService.MaximumAttempts)
                    {
                        await service.TryConsumeAsync(
                            "user-1",
                            reservation.ReservedValue);
                    }
                });

            await Task.WhenAll(requests);

            Assert.InRange(
                checkedGuessCount,
                1,
                PasswordResetOtpService.MaximumAttempts);
            Assert.True(
                string.IsNullOrWhiteSpace(store.Value) ||
                PasswordResetOtpState.TryParse(
                    store.Value,
                    out var state) &&
                state.Attempts >= PasswordResetOtpService.MaximumAttempts);
        }

        [Fact]
        public async Task FifthFailedAttemptPreventsAnotherReservation()
        {
            var now = DateTimeOffset.UtcNow;
            var state = new PasswordResetOtpState(
                now.AddMinutes(10),
                0,
                now,
                1,
                now,
                "hashed-otp");
            var store = new FakeOtpStore(state.Serialize());
            var service = new PasswordResetOtpService(store);

            for (var expectedAttempt = 1; expectedAttempt <= 5; expectedAttempt++)
            {
                var reservation = await service.TryReserveAttemptAsync(
                    "user-1",
                    now);

                Assert.True(reservation.IsReserved);
                Assert.Equal(expectedAttempt, reservation.State.Attempts);
                if (expectedAttempt == PasswordResetOtpService.MaximumAttempts)
                {
                    Assert.True(await service.TryConsumeAsync(
                        "user-1",
                        reservation.ReservedValue));
                }
            }

            var sixthAttempt = await service.TryReserveAttemptAsync(
                "user-1",
                now);

            Assert.False(sixthAttempt.IsReserved);
        }

        [Fact]
        public async Task ConsumingAnOtpPreventsReuse()
        {
            var now = DateTimeOffset.UtcNow;
            var state = new PasswordResetOtpState(
                now.AddMinutes(10),
                0,
                now,
                1,
                now,
                "hashed-otp");
            var store = new FakeOtpStore(state.Serialize());
            var service = new PasswordResetOtpService(store);

            var reservation = await service.TryReserveAttemptAsync(
                "user-1",
                now);
            Assert.True(reservation.IsReserved);
            Assert.True(await service.TryConsumeAsync(
                "user-1",
                reservation.ReservedValue));

            var reuse = await service.TryReserveAttemptAsync(
                "user-1",
                now);

            Assert.False(reuse.IsReserved);
        }

        [Fact]
        public async Task IssuanceCooldownSurvivesOtpConsumption()
        {
            var now = DateTimeOffset.FromUnixTimeSeconds(
                DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            var store = new FakeOtpStore(null);
            var service = new PasswordResetOtpService(store);
            var user = new ApplicationUser { Id = "user-1" };

            var firstIssuance = await service.TryReserveIssuanceAsync(
                user,
                now);
            Assert.True(firstIssuance.IsReserved);

            var otpState = new PasswordResetOtpState(
                now.AddMinutes(10),
                4,
                now,
                firstIssuance.IssuanceCount,
                now,
                "hashed-otp");
            var storeResult = await store.SetAsync(
                user,
                otpState.Serialize());
            Assert.True(storeResult.Succeeded);
            var otpReservation = await service.TryReserveAttemptAsync(
                user.Id,
                now);
            Assert.True(otpReservation.IsReserved);
            Assert.True(await service.TryConsumeAsync(
                user.Id,
                otpReservation.ReservedValue));

            var secondIssuance = await service.TryReserveIssuanceAsync(
                user,
                now.AddSeconds(30));

            Assert.False(secondIssuance.IsReserved);
            Assert.Equal(
                PasswordResetOtpIssuanceStatus.Cooldown,
                secondIssuance.Status);
        }

        private sealed class FakeOtpStore : IPasswordResetOtpStore
        {
            private readonly object _gate = new();
            private string _value;
            private string _rateLimitValue;

            public FakeOtpStore(string value)
            {
                _value = value;
            }

            public string Value
            {
                get
                {
                    lock (_gate)
                    {
                        return _value;
                    }
                }
            }

            public Task<string> GetAsync(
                string userId,
                CancellationToken cancellationToken = default)
            {
                lock (_gate)
                {
                    return Task.FromResult(_value);
                }
            }

            public Task<IdentityResult> SetAsync(
                ApplicationUser user,
                string value)
            {
                lock (_gate)
                {
                    _value = value;
                    return Task.FromResult(IdentityResult.Success);
                }
            }

            public Task<string> GetRateLimitAsync(
                string userId,
                CancellationToken cancellationToken = default)
            {
                lock (_gate)
                {
                    return Task.FromResult(_rateLimitValue);
                }
            }

            public Task<IdentityResult> SetRateLimitAsync(
                ApplicationUser user,
                string value)
            {
                lock (_gate)
                {
                    if (_rateLimitValue != null)
                    {
                        return Task.FromResult(
                            IdentityResult.Failed(
                                new IdentityError
                                {
                                    Code = "ConcurrencyFailure"
                                }));
                    }

                    _rateLimitValue = value;
                    return Task.FromResult(IdentityResult.Success);
                }
            }

            public Task<bool> TryReplaceRateLimitAsync(
                string userId,
                string expectedValue,
                string replacementValue,
                CancellationToken cancellationToken = default)
            {
                lock (_gate)
                {
                    if (_rateLimitValue != expectedValue)
                    {
                        return Task.FromResult(false);
                    }

                    _rateLimitValue = replacementValue;
                    return Task.FromResult(true);
                }
            }

            public Task<bool> TryReplaceAsync(
                string userId,
                string expectedValue,
                string replacementValue,
                CancellationToken cancellationToken = default)
            {
                lock (_gate)
                {
                    if (_value != expectedValue)
                    {
                        return Task.FromResult(false);
                    }

                    _value = replacementValue;
                    return Task.FromResult(true);
                }
            }

            public Task<bool> TryRemoveAsync(
                string userId,
                string expectedValue,
                CancellationToken cancellationToken = default)
            {
                lock (_gate)
                {
                    if (_value != expectedValue)
                    {
                        return Task.FromResult(false);
                    }

                    _value = null;
                    return Task.FromResult(true);
                }
            }
        }
    }
}
