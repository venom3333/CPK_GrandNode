using System;
using System.IO;
using System.Threading.Tasks;

namespace YandexObjectStorageService
{
    public interface IYandexStorageService
    {
        Task<bool> DeleteObjectAsync(string filename);
        Task<byte[]> GetAsByteArrayAsync(string filename);
        Task<Stream> GetAsStreamAsync(string filename);
        string GetPresignedUrlAsync(string filename, TimeSpan expirationTime = default);
        Task<string> PutObjectAsync(byte[] byteArr, string filename);
        Task<string> PutObjectAsync(Stream stream, string filename);
        Task<bool> TryGetAsync();
    }
}