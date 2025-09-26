using System;
using System.IO;

/// <summary>
/// InvoiceToArchiveStream methodunun kullanım örnekleri
/// </summary>
public class InvoiceToArchiveStreamUsage
{
    /// <summary>
    /// Örnek 1: Normal kullanım - InvoiceToArchiveOptimized ile
    /// </summary>
    public void Example1_NormalUsage()
    {
        // Önce normal methodu dene
        byte[] invoiceData = InvoiceToArchiveOptimized(invoiceType, version);
        
        if (invoiceData == null)
        {
            // Invoice çok büyükse, stream-based approach kullan
            invoiceData = CreateInvoiceArchiveWithStream(invoiceType, version);
        }
        
        // invoice.Data'ya ata
        invoice.Data = invoiceData;
    }
    
    /// <summary>
    /// Örnek 2: Stream-based approach ile archive oluştur
    /// </summary>
    public byte[] CreateInvoiceArchiveWithStream(InvoiceType invoiceType, IUBLTRVersion version)
    {
        string tempFilePath = null;
        try
        {
            // Temporary file oluştur
            tempFilePath = Path.GetTempFileName();
            
            // Invoice'ı file'a serialize et
            using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write))
            {
                InvoiceArchiveOptimized.InvoiceToArchiveStream(invoiceType, version, fileStream);
            }
            
            // File'ı oku ve archive oluştur
            using (var fileReadStream = new FileStream(tempFilePath, FileMode.Open, FileAccess.Read))
            {
                return OptimizedZipHelper.ArchiveFromStream(invoiceType.UUID.Value, fileReadStream);
            }
        }
        finally
        {
            // Temporary file'ı temizle
            if (!string.IsNullOrEmpty(tempFilePath) && File.Exists(tempFilePath))
            {
                try { File.Delete(tempFilePath); } catch { }
            }
        }
    }
    
    /// <summary>
    /// Örnek 3: Direkt stream'e yazma (çok büyük dosyalar için)
    /// </summary>
    public void Example3_DirectStreamWriting()
    {
        string outputFilePath = $"invoice_{invoiceType.UUID.Value}.zip";
        
        // Direkt file'a yaz
        using (var outputStream = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write))
        {
            // Invoice'ı stream'e serialize et
            InvoiceArchiveOptimized.InvoiceToArchiveStream(invoiceType, version, outputStream);
        }
        
        // File'ı oku ve invoice.Data'ya ata
        invoice.Data = File.ReadAllBytes(outputFilePath);
        
        // Temporary file'ı temizle
        try { File.Delete(outputFilePath); } catch { }
    }
    
    /// <summary>
    /// Örnek 4: Memory-efficient approach - tam implementasyon
    /// </summary>
    public void Example4_CompleteImplementation()
    {
        byte[] invoiceData = null;
        
        try
        {
            // Önce normal methodu dene (5MB limit ile)
            invoiceData = InvoiceToArchiveOptimized(invoiceType, version, 5 * 1024 * 1024);
            
            if (invoiceData == null)
            {
                // Invoice çok büyükse, stream-based approach kullan
                invoiceData = CreateInvoiceArchiveWithStream(invoiceType, version);
            }
            
            if (invoiceData == null)
            {
                // Hala null ise, direkt file'a yaz
                string tempFilePath = Path.GetTempFileName();
                try
                {
                    using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write))
                    {
                        InvoiceArchiveOptimized.InvoiceToArchiveStream(invoiceType, version, fileStream);
                    }
                    
                    invoiceData = File.ReadAllBytes(tempFilePath);
                }
                finally
                {
                    try { File.Delete(tempFilePath); } catch { }
                }
            }
        }
        catch (OutOfMemoryException)
        {
            // Memory yetersizse, stream-based approach kullan
            invoiceData = CreateInvoiceArchiveWithStream(invoiceType, version);
        }
        finally
        {
            // Memory temizle
            GC.Collect();
        }
        
        invoice.Data = invoiceData;
    }
    
    /// <summary>
    /// Örnek 5: Mevcut kodunuzu güncelleme
    /// </summary>
    public void Example5_UpdateYourCode()
    {
        // Mevcut kodunuz:
        // invoice.Data = InvoiceToArchive(invoiceType, version);
        
        // Güncellenmiş kod:
        byte[] invoiceData = null;
        
        try
        {
            // Önce optimize edilmiş methodu dene
            invoiceData = InvoiceToArchiveOptimized(invoiceType, version);
        }
        catch (OutOfMemoryException)
        {
            // Memory yetersizse, stream-based approach kullan
            invoiceData = CreateInvoiceArchiveWithStream(invoiceType, version);
        }
        
        if (invoiceData == null)
        {
            // Hala null ise, hata fırlat
            throw new InvalidOperationException("Invoice archive oluşturulamadı - dosya çok büyük");
        }
        
        invoice.Data = invoiceData;
    }
}

/// <summary>
/// InvoiceToArchiveStream methodunun implementasyonu
/// </summary>
public static class InvoiceArchiveOptimized
{
    /// <summary>
    /// Stream-based invoice serialization for very large invoices
    /// </summary>
    public static void InvoiceToArchiveStream(InvoiceType invoiceType, IUBLTRVersion version, Stream outputStream)
    {
        if (invoiceType == null || outputStream == null)
            return;
            
        UBLTRInvoiceSerializer invoiceSerializer = new UBLTRInvoiceSerializer(version);
        invoiceSerializer.Serialize(invoiceType, outputStream);
    }
    
    /// <summary>
    /// Optimize edilmiş InvoiceToArchive methodu
    /// </summary>
    public static byte[] InvoiceToArchiveOptimized(InvoiceType invoiceType, IUBLTRVersion version, int maxSizeBytes = 5 * 1024 * 1024)
    {
        if (invoiceType == null)
            return null;
            
        string tempFilePath = null;
        try
        {
            tempFilePath = Path.GetTempFileName();
            
            // File'a serialize et
            using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write))
            {
                UBLTRInvoiceSerializer invoiceSerializer = new UBLTRInvoiceSerializer(version);
                if (invoiceSerializer.Serialize(invoiceType, fileStream))
                {
                    // File size check
                    var fileInfo = new FileInfo(tempFilePath);
                    if (fileInfo.Length > maxSizeBytes)
                        return null; // Çok büyük, null döndür
                    
                    // Archive oluştur
                    using (var fileReadStream = new FileStream(tempFilePath, FileMode.Open, FileAccess.Read))
                    {
                        return OptimizedZipHelper.ArchiveFromStream(invoiceType.UUID.Value, fileReadStream);
                    }
                }
            }
        }
        finally
        {
            if (!string.IsNullOrEmpty(tempFilePath) && File.Exists(tempFilePath))
            {
                try { File.Delete(tempFilePath); } catch { }
            }
        }
        
        return null;
    }
}