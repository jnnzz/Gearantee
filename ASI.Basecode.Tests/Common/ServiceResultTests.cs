using ASI.Basecode.Services.Common;
using ASI.Basecode.Services.ServiceModels.Reservations;
using ASI.Basecode.Services.ServiceModels.Returns;
using Xunit;

namespace ASI.Basecode.Tests.Common
{
    /// <summary>
    /// Guards against re-introducing shadowed result properties. A result viewed through its
    /// <see cref="ServiceResult"/> base must report the same success flag and message as the
    /// derived type; otherwise callers that consume the base type silently see failure.
    /// </summary>
    public class ServiceResultTests
    {
        [Fact]
        public void ReservationResult_Success_IsVisibleThroughBaseType()
        {
            var result = ReservationResult.Success("Reservation request submitted.", 5);
            ServiceResult baseResult = result;

            Assert.True(result.Succeeded);
            Assert.True(baseResult.Succeeded);
            Assert.Equal(result.Message, baseResult.Message);
        }

        [Fact]
        public void ReturnRecordResult_Success_IsVisibleThroughBaseType()
        {
            var result = ReturnRecordResult.Success("Return recorded.", 7);
            ServiceResult baseResult = result;

            Assert.True(result.Succeeded);
            Assert.True(baseResult.Succeeded);
            Assert.Equal(result.Message, baseResult.Message);
        }

        [Fact]
        public void FailedResults_ExposeEmptyMessageThroughBaseType()
        {
            ServiceResult reservationFailure = ReservationResult.Fail("Nope.");
            ServiceResult returnFailure = ReturnRecordResult.Fail("Nope.");

            Assert.False(reservationFailure.Succeeded);
            Assert.Equal("Nope.", reservationFailure.Message);
            Assert.False(returnFailure.Succeeded);
            Assert.Equal("Nope.", returnFailure.Message);
        }
    }
}