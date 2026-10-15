# Zig 0.17 ile Git Aktivite Takvimi Geliştirmek

**Platform:** Windows Subsystem for Linux (WSL) / Ubuntu  
**Dil:** Zig 0.17.0  
**Seviye:** Başlangıç–Orta  
**Proje:** `git-activity`  
**Ön bilgiler:** Git, terminal ve temel programlama. Rust ve C# karşılaştırmaları içerir.

> **Not:** Bu doküman, Copilot CLI'daki aktivite takvimi görünümünden esinlenen yerel Git tabanlı bir CLI uygulamasının öğreticisidir. Kaynak kod Zig 0.17 API'leri dikkate alınarak hazırlanmıştır; hazırlık ortamında Zig derleyicisi bulunmadığından doğrudan derlenerek doğrulanamamıştır. Geliştirme ortamınızda komutları çalıştırıp gerekiyorsa hata mesajlarına göre düzenleme yapın.

## İçindekiler

1. [Projenin hedefi](#1-projenin-hedefi)
2. [Kullanacağımız teknoloji ve veri kaynağı](#2-kullanacağımız-teknoloji-ve-veri-kaynağı)
3. [WSL Ubuntu üzerinde Zig kurulumu](#3-wsl-ubuntu-üzerinde-zig-kurulumu)
4. [Proje oluşturma](#4-proje-oluşturma)
5. [Git commit verilerini okuma](#5-git-commit-verilerini-okuma)
6. [Uygulamanın mimarisi](#6-uygulamanın-mimarisi)
7. [Zig kodundaki önemli kavramlar](#7-zig-kodundaki-önemli-kavramlar)
8. [Uygulamanın kaynak kodu](#8-uygulamanın-kaynak-kodu)
9. [Derleme ve çalıştırma](#9-derleme-ve-çalıştırma)
10. [Testler ve sorun giderme](#10-testler-ve-sorun-giderme)
11. [Sonraki geliştirmeler](#11-sonraki-geliştirmeler)
12. [Öğrenme kontrol listesi ve kaynaklar](#12-öğrenme-kontrol-listesi-ve-kaynaklar)

---

## 1. Projenin hedefi

GitHub'ın *Contribution Graph* düzenine benzeyen bir aktivite takvimi üreten küçük bir komut satırı uygulaması geliştireceğiz.

Uygulama:

- Çalıştırıldığı Git deposunun commit geçmişini okuyacak.
- Son **180 takvim gününü** (bugün dahil) gösterecek.
- Günlük commit sayılarını hesaplayacak.
- Günleri 7 satırlı haftalık sütunlar hâlinde yerleştirecek.
- Aktiviteyi beş renk yoğunluğuyla terminale basacak.
- Ay adlarını, hafta içi günlerini, toplam commit sayısını ve renk ölçeğini gösterecek.
- İsteğe bağlı repository yolu kabul edecek.

Temsili metin yerleşimi (asıl program ANSI renkli hücreler kullanır):

```text
Activity · last 180 days · 153 commits

    Nis May Haz Tem Ağu Eyl Eki
Pzt ░  ░  ▒  ░  ░  ░  ▓ ...
    ░  ░  ░  ▒  ░  ░  ░ ...
Çar ░  ▓  ▒  █  ░  ░  ░ ...
    ░  ░  ░  ░  ░  ▒  ░ ...
Cum ░  ░  ▓  ░  ░  ░  ░ ...
    ░  ░  ░  ░  ░  ░  ░ ...
    ░  ░  ░  ░  ░  ░  ░ ...

    Less ░ ▒ ▓ █ More
```

**Önemli ayrım:** Bu örnek GitHub API'sine bağlanmaz. **Yerel Git geçmişindeki**, üzerinde bulunduğunuz dalın erişilebilir commit'lerini sayar. GitHub'ın contribution hesabı farklı kurallara sahip olabileceğinden sayılar eşleşmek zorunda değildir. Hesaplama **committer date** ve makinenin yerel saat dilimi esas alınarak yapılır.

## 2. Kullanacağımız teknoloji ve veri kaynağı

- **Zig 0.17.0:** CLI ve terminal çıktısı için.
- **Git:** Tarih bazlı commit verisini almak için.
- **GNU `date`:** WSL Ubuntu'da yerel tarih aralığının başlangıç ve bitişini belirlemek için.
- **ANSI escape sequences:** Renkli terminal hücrelerini göstermek için.

Başka bir Zig paketi, GitHub token'ı veya web servisi kullanmayacağız.

Zig'in 0.17 sürümü eski örneklerle uyumlu olmayabilecek I/O ve process API'leri içerdiğinden özellikle **0.17 için hazırlanmış sözdizimine** dikkat etmeliyiz.

## 3. WSL Ubuntu üzerinde Zig kurulumu

Windows Terminal'i açıp Ubuntu oturumunu başlatın. Ubuntu zaten kurulu değilse önce Windows'ta `wsl --install -d Ubuntu` adımını tamamlamak gerekir; bu rehber Ubuntu'nun hazır olduğunu varsayar.

### 3.1. Temel araçları yükleyelim

```bash
sudo apt update
sudo apt install -y git curl xz-utils ca-certificates coreutils
```

Doğrulama:

```bash
git --version
curl --version
uname -m
```

Son komut, çoğu Intel/AMD WSL kurulumunda `x86_64`, ARM sistemlerde `aarch64` döndürür.

### 3.2. Resmî Zig arşivini indirelim

En güncel sürüm için her zaman [Zig'in resmî indirme sayfasını](https://ziglang.org/download/) kontrol edin. Bu öğretici **0.17.0** sürümünü hedefler.

```bash
ZIG_VERSION="0.17.0"
ARCH="$(uname -m)"

mkdir -p "$HOME/.local/opt"

curl -fL \
  "https://ziglang.org/download/${ZIG_VERSION}/zig-${ARCH}-linux-${ZIG_VERSION}.tar.xz" \
  -o /tmp/zig.tar.xz
```

Arşivi açalım:

```bash
tar -xJf /tmp/zig.tar.xz -C "$HOME/.local/opt"
```

`PATH` değişkenine ekleyelim:

```bash
ZIG_HOME="$HOME/.local/opt/zig-${ARCH}-linux-${ZIG_VERSION}"

printf '\nexport PATH="%s:$PATH"\n' "$ZIG_HOME" >> ~/.bashrc
export PATH="$ZIG_HOME:$PATH"
```

`~/.bashrc` kalıcı ayar içindir; `export` ise mevcut terminal oturumunu günceller.

> **Güvenlik önerisi:** İndirdiğiniz arşivin SHA-256 hash'ini ya da yayımlanmış imzasını resmî Zig indirme sayfasındaki değerle karşılaştırın.

### 3.3. Kurulumu sınayalım

```bash
zig version
zig env
```

`zig version` çıktısının `0.17.0` olması beklenir.

## 4. Proje oluşturma

```bash
mkdir -p ~/projects/git-activity
cd ~/projects/git-activity
zig init
```

Başlangıçta beklenen yapı:

```text
git-activity/
├── build.zig
├── build.zig.zon
└── src/
    ├── main.zig
    └── root.zig
```

Kontrol:

```bash
zig build
zig build run
```

Daha sonra `src/main.zig` dosyasını 8. bölümdeki uygulama koduyla değiştireceğiz.

### Rust ve C# ile karşılaştırma

| İşlem | Rust | C# | Zig |
| --- | --- | --- | --- |
| Proje başlatma | `cargo init` | `dotnet new console` | `zig init` |
| Derleme | `cargo build` | `dotnet build` | `zig build` |
| Çalıştırma | `cargo run` | `dotnet run` | `zig build run` |
| Test | `cargo test` | `dotnet test` | `zig build test` |
| Tek dosya çalıştırma | — | Duruma göre | `zig run main.zig` |

Zig'i öğrenirken iki kavram özellikle önemlidir: **allocator yönetimi** ve **açık I/O bağlamı**.

## 5. Git commit verilerini okuma

Aşağıdaki komutu bir Git repository'sinde deneyin:

```bash
git log \
  --since="180 days ago" \
  --format="%cd" \
  --date=format-local:%Y-%m-%d
```

Örnek çıktı:

```text
2026-10-15
2026-10-15
2026-10-14
2026-10-12
2026-10-12
2026-10-12
2026-10-09
```

Bunları grupladığımızda:

| Tarih | Commit sayısı |
| --- | ---: |
| 2026-10-15 | 2 |
| 2026-10-14 | 1 |
| 2026-10-13 | 0 |
| 2026-10-12 | 3 |
| 2026-10-09 | 1 |

- `%cd`, commit'in **committer date** alanıdır.
- `--date=format-local:%Y-%m-%d`, makinenin yerel zaman dilimindeki tarihi üretir.
- `--since`, log taramasının başlangıç sınırını belirler.

Asıl program `date -d "179 days ago" +%F` kullanarak **bugün dahil 180 ayrı günü** kapsayan ilk tarihi belirler; ayrıca tarihleri Zig içinde gün numaralarına dönüştürerek aralığı kesin olarak filtreler.

## 6. Uygulamanın mimarisi

İlk versiyon için gereksiz katmanlar kurmadan dört aşamalı bir akış kullanacağız:

```text
┌──────────────────────────┐
│ CLI Entry Point          │
│ Argümanları oku          │
└────────────┬─────────────┘
             │
             ▼
┌──────────────────────────┐
│ Git Process              │
│ Commit tarihlerini al    │
└────────────┬─────────────┘
             │
             ▼
┌──────────────────────────┐
│ Activity Aggregation     │
│ 180 günlük sayım dizisi  │
└────────────┬─────────────┘
             │
             ▼
┌──────────────────────────┐
│ Terminal Renderer        │
│ ANSI renkli takvim       │
└──────────────────────────┘
```

Bütün kodu şimdilik tek dosyada tutacağız. Sonradan `git.zig`, `calendar.zig` ve `renderer.zig` modüllerine ayırabiliriz.

## 7. Zig kodundaki önemli kavramlar

### 7.1. `main`, allocator ve I/O

```zig
const std = @import("std");

pub fn main(init: std.process.Init) !void {
    const allocator = init.gpa;
    const io = init.io;

    // Uygulama kodu...
}
```

| Zig | Rust/C# benzetmesi |
| --- | --- |
| `std.process.Init` | Uygulama başlangıç bağlamı |
| `init.gpa` | Uygulama için bellek ayırıcısı |
| `init.io` | İşlemler için I/O bağlamı |
| `!void` | Hata döndürebilen fonksiyon |
| `try` | Rust `?` operatörü gibi hata iletimi |
| `defer` | Kapsam sonunda temizleme: RAII / `finally` benzeri |

Rust'ta bellek çoğunlukla `Drop` ile serbest bırakılır. Zig'de bellek ayırıcısının ve temizlemenin açıkça belirtilmesi daha yaygındır.

### 7.2. Dış process çalıştırma

Rust'ta `std::process::Command`, C#'ta `System.Diagnostics.Process` kullanırız. Zig 0.17 için örnek:

```zig
const result = try std.process.run(
    allocator,
    io,
    .{
        .argv = &.{ "git", "log", "-5", "--oneline" },
    },
);

defer allocator.free(result.stdout);
defer allocator.free(result.stderr);

if (!result.term.success()) {
    return error.GitCommandFailed;
}
```

Process'in standart çıktısı ve hata çıktısı belleğe alınır; iki buffer'ın temizlenmesi çağıranın sorumluluğundadır.

### 7.3. Commit sayılarını saklama

180 sabit gün için HashMap gerekli değil:

```zig
const days_visible: usize = 180;
var counts: [days_visible]u32 = @splat(0);
```

Bir commit tarihi, ilk günden itibaren kaçıncı güne denk geliyorsa ilgili indis artırılır:

```zig
const index: usize = @intCast(day_number - first_day);
counts[index] += 1;
```

İndisler `0` (179 gün önce) ile `179` (bugün) arasında değişir. `@splat(0)` bütün elemanları sıfırlar.

### 7.4. Tarihleri gün numarasına dönüştürme

Bir tarihi örneğin `2026-10-15` metninden, 1970-01-01 başlangıcına göre gün sayısına dönüştürürüz.

```zig
fn daysFromCivil(year: i64, month: i64, day: i64) i64
fn civilFromDays(days: i64) Date
```

Bunlar Howard Hinnant'ın [Civil Date Algorithms](https://howardhinnant.github.io/date_algorithms.html) yaklaşımını temel alır.

Haftanın günü:

```zig
const weekday = @mod(day_number + 4, 7);
```

`0 = Pazar`, `1 = Pazartesi`, `6 = Cumartesi`. `+4` sabiti, 1970-01-01'in perşembe oluşundan gelir.

### 7.5. Aktivite yoğunluğu ve renk eşikleri

| Günlük commit | Seviye |
| ---: | ---: |
| 0 | 0 — boş |
| 1 | 1 — düşük |
| 2–3 | 2 — orta |
| 4–6 | 3 — yüksek |
| 7+ | 4 — çok yüksek |

```zig
fn colorLevel(commits: u32) usize {
    if (commits == 0) return 0;
    if (commits == 1) return 1;
    if (commits <= 3) return 2;
    if (commits <= 6) return 3;
    return 4;
}
```

Bunlar tercih ettiğimiz sabit eşiklerdir. Gelecekte repository büyüklüğüne göre dinamik hâle getirilebilirler.

### 7.6. ANSI ile terminale renk basma

```zig
const colors = [_][]const u8{
    "\x1b[48;5;236m", // Aktivite yok
    "\x1b[48;5;23m",  // 1 commit
    "\x1b[48;5;30m",  // 2-3 commit
    "\x1b[48;5;37m",  // 4-6 commit
    "\x1b[48;5;51m",  // 7+ commit
};
```

`48;5` ANSI 256-renk paletinden **arka plan** rengi seçer. Hücre şu şekilde yazılır:

```zig
try out.print("{s}  \x1b[0m ", .{colors[level]});
```

İki boşluk renkli bir kare gibi görünür, `\x1b[0m` ise rengi sıfırlar. `out` gerçek uygulamada bir `std.Io.Writer` referansıdır ve çıktı işleminden sonra `flush()` gerekir.

## 8. Uygulamanın kaynak kodu

Aşağıdaki dosyanın tamamını `src/main.zig` olarak kaydedin.

> **Bağımlılıklar:** `git` ve Ubuntu'daki GNU `date` komutları `PATH` üzerinde olmalıdır. Kod harici bir Zig paketi istemez.

```zig
const std = @import("std");

const days_visible: usize = 180;
const month_names = [_][]const u8{
    "Oca", "Şub", "Mar", "Nis", "May", "Haz",
    "Tem", "Ağu", "Eyl", "Eki", "Kas", "Ara",
};
const colors = [_][]const u8{
    "\x1b[48;5;236m", // no activity
    "\x1b[48;5;23m",  // 1 commit
    "\x1b[48;5;30m",  // 2-3 commits
    "\x1b[48;5;37m",  // 4-6 commits
    "\x1b[48;5;51m",  // 7+ commits
};

const Date = struct {
    year: i64,
    month: usize,
    day: usize,
};

pub fn main(init: std.process.Init) !void {
    const allocator = init.gpa;
    const io = init.io;

    var args = init.minimal.args.iterate();
    _ = args.next(); // executable name
    const repo_path: []const u8 = args.next() orelse ".";
    if (args.next() != null) {
        std.debug.print("Kullanim: git-activity [repo-yolu]\n", .{});
        return error.TooManyArguments;
    }

    // GNU date is available on Ubuntu/WSL.
    const today_result = try std.process.run(allocator, io, .{
        .argv = &.{ "date", "+%F" },
    });
    defer allocator.free(today_result.stdout);
    defer allocator.free(today_result.stderr);
    if (!today_result.term.success()) return error.DateCommandFailed;

    const start_result = try std.process.run(allocator, io, .{
        .argv = &.{ "date", "-d", "179 days ago", "+%F" },
    });
    defer allocator.free(start_result.stdout);
    defer allocator.free(start_result.stderr);
    if (!start_result.term.success()) return error.DateCommandFailed;

    const today_date = std.mem.trim(u8, today_result.stdout, " \t\r\n");
    const start_date = std.mem.trim(u8, start_result.stdout, " \t\r\n");
    const last_day = try parseDay(today_date);
    const first_day = last_day - @as(i64, days_visible - 1);

    // At local midnight, --since includes the entire first day.
    const since_arg = try allocator.print("--since={s}", .{start_date});
    defer allocator.free(since_arg);

    const git_result = try std.process.run(allocator, io, .{
        .argv = &.{
            "git", "-C", repo_path, "log", since_arg,
            "--format=%cd", "--date=format-local:%Y-%m-%d",
        },
    });
    defer allocator.free(git_result.stdout);
    defer allocator.free(git_result.stderr);

    if (!git_result.term.success()) {
        std.debug.print("Git hatasi: {s}\n", .{git_result.stderr});
        return error.GitCommandFailed;
    }

    var counts: [days_visible]u32 = @splat(0);
    var total: u64 = 0;
    var lines = std.mem.tokenizeScalar(u8, git_result.stdout, '\n');
    while (lines.next()) |raw_line| {
        const line = std.mem.trim(u8, raw_line, " \t\r");
        const day_number = try parseDay(line);
        if (day_number < first_day or day_number > last_day) continue;

        const index: usize = @intCast(day_number - first_day);
        counts[index] += 1;
        total += 1;
    }

    var output_buffer: [4096]u8 = undefined;
    var file_writer = std.Io.File.stdout().writer(io, &output_buffer);
    const out = &file_writer.interface;
    try render(out, counts[0..], first_day, last_day, total);
    try out.flush();
}

fn render(out: *std.Io.Writer, counts: []const u32, first_day: i64, last_day: i64, total: u64) !void {
    // Sunday = 0, Monday = 1, ... Saturday = 6.
    const first_sunday = first_day - @mod(first_day + 4, 7);
    const week_count: usize = @intCast(@divTrunc(last_day - first_sunday, 7) + 1);

    try out.print("\n  Activity · last {d} days · {d} commits\n\n", .{ days_visible, total });

    // Month labels, one 3-column slot per week.
    try out.writeAll("    ");
    for (0..week_count) |week| {
        const sunday = first_sunday + @as(i64, @intCast(week * 7));
        var month_label: []const u8 = "   ";
        for (0..7) |weekday| {
            const current = sunday + @as(i64, @intCast(weekday));
            if (current < first_day or current > last_day) continue;
            const date = civilFromDays(current);
            if (date.day == 1) month_label = month_names[date.month - 1];
        }
        try out.print("{s}", .{month_label});
    }
    try out.writeAll("\n");

    const labels = [_][]const u8{ "   ", "Pzt", "   ", "Çar", "   ", "Cum", "   " };
    for (0..7) |weekday| {
        try out.print("{s} ", .{labels[weekday]});
        for (0..week_count) |week| {
            const day_number = first_sunday + @as(i64, @intCast(week * 7 + weekday));
            if (day_number < first_day or day_number > last_day) {
                try out.writeAll("   ");
                continue;
            }
            const index: usize = @intCast(day_number - first_day);
            const level = colorLevel(counts[index]);
            try out.print("{s}  \x1b[0m ", .{colors[level]});
        }
        try out.writeAll("\n");
    }

    try out.writeAll("\n    Less ");
    for (colors) |color| {
        try out.print("{s}  \x1b[0m ", .{color});
    }
    try out.writeAll(" More\n\n");
}

fn colorLevel(commits: u32) usize {
    if (commits == 0) return 0;
    if (commits == 1) return 1;
    if (commits <= 3) return 2;
    if (commits <= 6) return 3;
    return 4;
}

fn parseDay(value: []const u8) !i64 {
    if (value.len != 10 or value[4] != '-' or value[7] != '-') return error.InvalidDate;
    const year = try std.fmt.parseInt(i64, value[0..4], 10);
    const month = try std.fmt.parseInt(i64, value[5..7], 10);
    const day = try std.fmt.parseInt(i64, value[8..10], 10);
    if (month < 1 or month > 12 or day < 1 or day > 31) return error.InvalidDate;
    return daysFromCivil(year, month, day);
}

// Howard Hinnant's Gregorian civil-date algorithms.
// Convert a civil date to days since 1970-01-01.
fn daysFromCivil(year: i64, month: i64, day: i64) i64 {
    const y = year - @as(i64, @intFromBool(month <= 2));
    const era = @divFloor(y, 400);
    const yoe = y - era * 400;
    const mp = month + (if (month > 2) @as(i64, -3) else @as(i64, 9));
    const doy = @divTrunc(153 * mp + 2, 5) + day - 1;
    const doe = yoe * 365 + @divTrunc(yoe, 4) - @divTrunc(yoe, 100) + doy;
    return era * 146097 + doe - 719468;
}

// Convert days since 1970-01-01 back to a civil date.
fn civilFromDays(days: i64) Date {
    const z = days + 719468;
    const era = @divFloor(z, 146097);
    const doe = z - era * 146097;
    const yoe = @divTrunc(doe - @divTrunc(doe, 1460) + @divTrunc(doe, 36524) - @divTrunc(doe, 146096), 365);
    var year = yoe + era * 400;
    const doy = doe - (365 * yoe + @divTrunc(yoe, 4) - @divTrunc(yoe, 100));
    const mp = @divTrunc(5 * doy + 2, 153);
    const day = doy - @divTrunc(153 * mp + 2, 5) + 1;
    const month = mp + (if (mp < 10) @as(i64, 3) else @as(i64, -9));
    if (month <= 2) year += 1;
    return .{ .year = year, .month = @intCast(month), .day = @intCast(day) };
}

test "days from civil handles epoch and leap years" {
    try std.testing.expectEqual(@as(i64, 0), daysFromCivil(1970, 1, 1));
    const leap = daysFromCivil(2024, 2, 29);
    const reconstructed = civilFromDays(leap);
    try std.testing.expectEqual(@as(i64, 2024), reconstructed.year);
    try std.testing.expectEqual(@as(usize, 2), reconstructed.month);
    try std.testing.expectEqual(@as(usize, 29), reconstructed.day);
}

test "activity buckets" {
    try std.testing.expectEqual(@as(usize, 0), colorLevel(0));
    try std.testing.expectEqual(@as(usize, 1), colorLevel(1));
    try std.testing.expectEqual(@as(usize, 2), colorLevel(3));
    try std.testing.expectEqual(@as(usize, 3), colorLevel(5));
    try std.testing.expectEqual(@as(usize, 4), colorLevel(7));
}
```

## 9. Derleme ve çalıştırma

Önce Git deposunun yolunu belirterek doğrudan çalıştırın:

```bash
cd ~/projects/git-activity
zig run src/main.zig -- ~/projects/my-rust-project
```

`--` işaretinden sonra gelen argümanlar Zig derleyicisine değil uygulamaya iletilir.

Uygulama bir Git repository'sinin **içinde** başlatılmışsa yol vermeniz gerekmez:

```bash
zig run src/main.zig
```

Bağımsız Linux çalıştırılabilir dosyasını üretelim:

```bash
zig build-exe src/main.zig -Ofast -femit-bin=git-activity
./git-activity ~/projects/my-rust-project
```

Binary'yi kullanıcıya ait `bin` dizinine taşımak için:

```bash
mkdir -p ~/.local/bin
cp git-activity ~/.local/bin/
```

Gerekirse `~/.local/bin` dizinini `PATH` değişkenine ekleyin. Daha sonra:

```bash
git-activity ~/projects/my-rust-project
```

> İlk `zig init` iskeletinin `zig build` işlemi, `src/main.zig` değiştirildikten sonra da normalde kullanılabilir. Bu öğreticide bağımsız dosyayı derlemek için `zig build-exe` komutunu özellikle gösteriyoruz.

## 10. Testler ve sorun giderme

Kodun sonunda iki Zig birim testi vardır:

1. Tarih dönüşümü: epoch tarihi ve artık yıl (`2024-02-29`).
2. `colorLevel` fonksiyonu: beş renk düzeyinden örnekler.

Testleri ve formatlamayı çalıştırın:

```bash
zig test src/main.zig
zig fmt src/main.zig
```

### Yaygın sorunlar

**`zig: command not found`** — `PATH` içeriğini kontrol edin; terminali yeniden açın veya `source ~/.bashrc` çalıştırın.

**`git: not a git repository`** — Repo dizinini doğru verdiğinizi test edin:

```bash
git -C ~/projects/my-rust-project rev-parse --is-inside-work-tree
```

**Boş aktivite takvimi** — Depoda seçilen 180 günlük dönemde erişilebilir commit bulunmayabilir. Şu komutla veriyi ayrıca kontrol edin:

```bash
git -C ~/projects/my-rust-project log -5 --format='%cd' --date=format-local:%Y-%m-%d
```

**Renkler görünmüyor** — ANSI 256 renk desteği olan bir terminal kullanın. WSL içindeki Windows Terminal ya da VS Code terminali uygun seçeneklerdir.

**Derleyici sürümüne bağlı hata** — Önce `zig version` kontrol edin. Bu dokümandaki kod Zig **0.17.0** hedeflenerek yazılmıştır; Zig sürümleri arasında standart kütüphane arayüzleri değişebilir.

## 11. Sonraki geliştirmeler

| Özellik | Öğretici kazanım |
| --- | --- |
| `--days 365` | Argüman işleme ve dinamik dizi boyutu |
| `--author email` | Git filtreleri / string işlemleri |
| `--no-color` | Terminal yapılandırması |
| `--stats` | Toplam, ortalama, en aktif gün |
| `--json` | Serileştirme |
| `--theme github` | `enum`, yapılandırma |
| GitHub API | HTTP, JSON, allocator yönetimi |
| Birden fazla repository | Veri birleştirme, modülerlik |

### Eklenen ve silinen satır sayılarını göstermek

Paylaştığınız Copilot CLI örneğindeki `Changes +418 -95` gibi bir özet için şu Git komutu kullanılabilir:

```bash
git log \
  --since="180 days ago" \
  --numstat \
  --format=tformat:
```

`--numstat` çıktılarını toplayarak eklenen ve silinen satırları hesaplayabiliriz. İkili (binary) dosyalar için satır sayısı üretilemeyebileceğini unutmayın.

## 12. Öğrenme kontrol listesi ve kaynaklar

- [ ] Allocator ve bellek sahipliği
- [ ] Error handling (`try`, `!void`)
- [ ] Slice ve sabit boyutlu diziler
- [ ] Process yönetimi (`std.process.run`)
- [ ] Writer ve buffered I/O
- [ ] Tarih hesaplamaları
- [ ] ANSI terminal renkleri
- [ ] Unit testing
- [ ] CLI argümanları ve çalıştırılabilir dosya oluşturma

### Kaynaklar

- [Zig — Resmî web sitesi](https://ziglang.org/)
- [Zig — İndirme sayfası](https://ziglang.org/download/)
- [Zig 0.17.0 — Dil referansı](https://ziglang.org/documentation/0.17.0/)
- [Zig 0.17.0 — Standart kütüphane](https://ziglang.org/documentation/0.17.0/std/)
- [Howard Hinnant — Date Algorithms](https://howardhinnant.github.io/date_algorithms.html)
- [Git log — Resmî dokümantasyon](https://git-scm.com/docs/git-log)

---

**Önerilen öğrenme sırası:** Önce programı çalıştırın; ardından `colorLevel` ve tarih algoritmasını testlerle anlamaya çalışın. Sonrasında Git çağrısını ve terminal renderer'ını ayrı `.zig` modüllerine bölerek refactoring yapın.
