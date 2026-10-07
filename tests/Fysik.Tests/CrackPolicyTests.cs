using Fysik.Structure;
using Xunit;

namespace Fysik.Tests
{
    public class CrackPolicyTests
    {
        private const double Warning = 5;

        private static CrackAction Decide(double u, bool cracking = false, double seconds = 0, bool cascading = false,
                                          bool ready = true, bool canFall = true, bool unsupported = false) =>
            CrackPolicy.Decide(u, unsupported, cracking, seconds, Warning, cascading, ready, canFall);

        [Fact]
        public void Piece_within_limits_does_nothing() => Assert.Equal(CrackAction.None, Decide(0.8));

        [Fact]
        public void Overloaded_piece_cracks_first() => Assert.Equal(CrackAction.StartCrack, Decide(1.3));

        [Fact]
        public void Piece_with_no_path_to_the_ground_falls_at_once()
        {
            Assert.Equal(CrackAction.Fall, Decide(double.PositiveInfinity, unsupported: true));
            Assert.Equal(CrackAction.Fall, Decide(double.PositiveInfinity, unsupported: true, cracking: true, seconds: 1));
        }

        [Fact]
        public void Unsupported_piece_still_waits_for_its_structure_to_load() =>
            Assert.Equal(CrackAction.None, Decide(double.PositiveInfinity, unsupported: true, ready: false));

        [Fact]
        public void Cracking_piece_waits_for_the_warning_time()
        {
            Assert.Equal(CrackAction.None, Decide(1.3, cracking: true, seconds: 4.9));
            Assert.Equal(CrackAction.Fall, Decide(1.3, cracking: true, seconds: 5));
        }

        [Fact]
        public void Shoring_up_a_cracking_piece_stops_the_crack() =>
            Assert.Equal(CrackAction.StopCrack, Decide(0.7, cracking: true, seconds: 3));

        [Fact]
        public void During_a_cascade_overloaded_pieces_fall_at_once()
        {
            Assert.Equal(CrackAction.Fall, Decide(1.1, cascading: true));
            Assert.Equal(CrackAction.None, Decide(0.9, cascading: true));
        }

        [Fact]
        public void Nothing_happens_while_the_structure_is_still_loading() =>
            Assert.Equal(CrackAction.None, Decide(5, cracking: true, seconds: 60, ready: false));

        [Fact]
        public void Pieces_that_cannot_fall_never_crack()
        {
            Assert.Equal(CrackAction.None, Decide(3, canFall: false));
            Assert.Equal(CrackAction.StopCrack, Decide(3, cracking: true, canFall: false));
        }

        [Fact]
        public void Zero_warning_time_means_falling_immediately() =>
            Assert.Equal(CrackAction.Fall, CrackPolicy.Decide(1.2, false, false, 0, 0, false, true, true));
    }
}
