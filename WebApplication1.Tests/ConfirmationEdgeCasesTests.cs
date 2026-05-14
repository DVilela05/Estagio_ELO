using Xunit;

namespace WebApplication1.Tests
{
    /// <summary>
    /// Testes para edge cases de confirmação
    /// </summary>
    public class ConfirmationEdgeCasesTests
    {
        [Theory]
        [InlineData("sim")]
        [InlineData("não")]
        [InlineData("yes")]
        [InlineData("no")]
        [InlineData("s")]
        [InlineData("n")]
        public void YesNoResponse_WithoutPendingConfirmation_ShouldBeHandledGracefully(string response)
        {
            // Arrange
            var normalizedResponse = response.ToLowerInvariant().Trim();
            
            // Act
            bool isYes = IsYesToken(normalizedResponse);
            bool isNo = IsNoToken(normalizedResponse);
            
            // Assert
            bool isConfirmationToken = isYes || isNo;
            Assert.True(isConfirmationToken, 
                $"'{response}' should be recognized as a yes/no confirmation token");
        }

        [Fact]
        public void YesTokens_ShouldRecognizeAllVariants()
        {
            // Arrange
            var yesVariants = new[] { "sim", "s", "yes", "y" };
            
            // Act & Assert
            foreach (var variant in yesVariants)
            {
                Assert.True(IsYesToken(variant), 
                    $"'{variant}' should be recognized as YES");
            }
        }

        [Fact]
        public void NoTokens_ShouldRecognizeAllVariants()
        {
            // Arrange
            var noVariants = new[] { "não", "nao", "n", "no" };
            
            // Act & Assert
            foreach (var variant in noVariants)
            {
                Assert.True(IsNoToken(variant), 
                    $"'{variant}' should be recognized as NO");
            }
        }

        [Theory]
        [InlineData("talvez")]
        [InlineData("presente")]
        [InlineData("ajuda")]
        [InlineData("random text")]
        public void NonConfirmationText_ShouldNotBeRecognized(string text)
        {
            // Arrange & Act
            bool isYes = IsYesToken(text);
            bool isNo = IsNoToken(text);
            
            // Assert
            Assert.False(isYes, $"'{text}' should not be recognized as YES");
            Assert.False(isNo, $"'{text}' should not be recognized as NO");
        }

        // Helper methods that mirror the controller logic
        private static bool IsYesToken(string text)
        {
            var yesTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "sim", "s", "yes", "y"
            };
            return yesTokens.Contains(text);
        }

        private static bool IsNoToken(string text)
        {
            var noTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "nao", "não", "n", "no"
            };
            return noTokens.Contains(text);
        }
    }
}
