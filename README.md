# 2024 - Bireysel Hedef

## Kod analizi, test (birim, entegrasyon, uçtan uca) ve sürekli entegrasyon/sürekli dağıtım (CI/CD) araçları ve teknikleri - Temiz kod yazma pratiğini - API geliştirmeye yönelik tasarım desenlerini öğrenme
### Kod Kalitesini İyileştirme

#### TaskManager/
│
├── TaskManager.Api/                // API projesi
│   ├── Controllers/                 // API denetleyicileri
│   ├── Models/                      // Veri modelleri
│   ├── Services/                    // İş mantığı
│   ├── Data/                        // Veri erişim katmanı
│   ├── TaskManager.Api.csproj
│   └── appsettings.json
│
├── TaskManager.Client/              // Blazor istemci projesi
│   ├── Pages/                       // Blazor sayfaları
│   ├── Components/                  // Blazor bileşenleri
│   ├── wwwroot/                     // Statik dosyalar
│   ├── TaskManager.Client.csproj
│   └── appsettings.json
│
├── TaskManager.Tests/               // Test projesi
│   ├── UnitTests/                   // Birim testleri
│   ├── IntegrationTests/            // Entegrasyon testleri
│   ├── TaskManager.Tests.csproj
│
└── TaskManager.sln                  // Çözüm dosyası


### Teknolojiler

**Blazor WebAssembly:** Kullanıcı arayüzü için.
**ASP.NET Core Web API:** Backend API geliştirmek için.
**Entity Framework Core:** Veritabanı erişimi için.
**xUnit:** Birim testleri için.
**Moq:** Mock nesneleri oluşturmak için. 