using System.Text.RegularExpressions;

namespace WebApplication1.Tests
{
    public class TextNormalizationTests
    {
        private static string NormalizeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            // Remove tudo exceto letras e espaços (remove números, emojis, pontuação)
            var regex1 = new Regex(@"[^\p{L}\s]");
            text = regex1.Replace(text, " ");

            text = text.ToLowerInvariant();

            var regex2 = new Regex(@"\s+");
            text = regex2.Replace(text, " ").Trim();

            return text;
        }

        [Theory]
        [InlineData("PRESENTE", "presente")]
        [InlineData("Presente", "presente")]
        [InlineData("AJUDA", "ajuda")]
        public void NormalizeText_WithDifferentCases_ReturnsLowercase(string input, string expected)
        {
            // Act
            var result = NormalizeText(input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("oi 123", "oi")]
        [InlineData("teste 456 abc", "teste abc")]
        [InlineData("2025 foi um ano", "foi um ano")]
        public void NormalizeText_WithNumbers_RemovesNumbers(string input, string expected)
        {
            // Act
            var result = NormalizeText(input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("olá!", "olá")]
        [InlineData("teste?", "teste")]
        [InlineData("mensagem,com pontuação.", "mensagem com pontuação")]
        public void NormalizeText_WithPunctuation_RemovesPunctuation(string input, string expected)
        {
            // Act
            var result = NormalizeText(input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("oi  👋", "oi")]
        [InlineData("😊 hello 🎉", "hello")]
        public void NormalizeText_WithEmojis_RemovesEmojis(string input, string expected)
        {
            // Act
            var result = NormalizeText(input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("texto   com   espaços", "texto com espaços")]
        [InlineData("  inicio com espaço", "inicio com espaço")]
        [InlineData("fim com espaço  ", "fim com espaço")]
        public void NormalizeText_WithMultipleSpaces_NormalizesSpaces(string input, string expected)
        {
            // Act
            var result = NormalizeText(input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void NormalizeText_WithNull_ReturnsEmpty()
        {
            // Act
            var result = NormalizeText(null!);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void NormalizeText_WithEmptyString_ReturnsEmpty()
        {
            // Act
            var result = NormalizeText("");

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void NormalizeText_WithWhitespaceOnly_ReturnsEmpty()
        {
            // Act
            var result = NormalizeText("   ");

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void NormalizeText_WithComplexInput_NormalizesCorrectly()
        {
            // Arrange
            var input = "  Olá!!!   MUNDO   😊🎉 ";
            var expected = "olá mundo";

            // Act
            var result = NormalizeText(input);

            // Assert
            Assert.Equal(expected, result);
        }
    }
}
