# Lunar Race - Sabitler ve Yapılandırma
import pygame

# Ekran boyutları
SCREEN_WIDTH = 900
SCREEN_HEIGHT = 550  # Arttırıldı
FPS = 60

# UI boyutları
TOP_BAR_HEIGHT = 60  # Üst bar yüksekliği artırıldı
BOTTOM_BAR_HEIGHT = 50  # Alt bar yüksekliği artırıldı

# Renkler (vektör çizgiler için)
WHITE = (255, 255, 255)
BLACK = (0, 0, 0)
GREEN = (0, 255, 0)
RED = (255, 0, 0)
BLUE = (0, 0, 255)
YELLOW = (255, 255, 0)
CYAN = (0, 255, 255)
MAGENTA = (255, 0, 255)
GRAY = (128, 128, 128)
ORANGE = (255, 165, 0)

# Oyun sabitleri
INITIAL_SPACERON = 10000
INITIAL_CELLURON = 1000
WARP_DURATION = 5  # saniye
WARP_COOLDOWN = 60  # saniye
CELLURON_DECREASE_RATE = 1  # her 5 saniyede 1 birim
ASTEROID_DAMAGE = 15  # göktaşı hasar yüzdesi
BLACKHOLE_DAMAGE = 50  # yüzde hasar
COMET_DAMAGE = 100  # yüzde hasar (oyun biter)

# Mekik hareket hızları
SHIP_NORMAL_SPEED = 3
SHIP_WARP_SPEED = 8
SHIP_DAMAGED_SPEED = 1.5

# Aktör hızları
ASTEROID_SPEED_MIN = 1
ASTEROID_SPEED_MAX = 4
REPAIR_SHIP_SPEED = 2
COMET_SPEED = 6
TRADE_STATION_SPEED = 1

# Maksimum aktör sayıları
MAX_ASTEROIDS = 3
MAX_PLANETS = 2
MAX_REPAIR_SHIPS = 1

# Klavye tuşları
KEYS = {
    'UP': pygame.K_w,
    'DOWN': pygame.K_s,
    'LEFT': pygame.K_a,
    'RIGHT': pygame.K_d,
    'WARP': pygame.K_SPACE,
    'UP_ARROW': pygame.K_UP,
    'DOWN_ARROW': pygame.K_DOWN,
    'LEFT_ARROW': pygame.K_LEFT,
    'RIGHT_ARROW': pygame.K_RIGHT
}

# Mineral türleri
MINERALS = [
    'Çelik', 'Oksijen', 'Tungsten', 'Gümüş', 'Mavi Kristal',
    'Kayber Kristal', 'İridyum', 'Demir', 'Silisyum', 'U-235',
    'Zephyrium', 'Quantum Çekirdeği', 'Stardust'
]

# Yeni mineral açıklamaları
MINERAL_DESCRIPTIONS = {
    'Zephyrium': 'Hafif ama sağlam, uzay gemilerinin ana yapı malzemesi',
    'Quantum Çekirdeği': 'Quantum teknolojisinin temeli, son derece nadir',
    'Stardust': 'Yıldız tozu, keşif teknolojilerinde kullanılan mistik madde'
}

# Gezegen adları
PLANET_NAMES = [
    'Grenwood', 'Alpha 356', 'Odsseyy Hole', 'Shock Hole', 'Black Widow',
    'Redroom', 'Blinky Shadow', 'Tatuyin', 'S-Bull', 'Hardal',
    'Edge of the Universe', 'Crimson Moon', 'Silent Void', 'Nova Prime',
    'Stellar Gate', 'Crystal Bay', 'Iron Forge', 'Golden Sands',
    'Dark Matter', 'Quantum Realm', 'Nebula Station', 'Void Walker',
    'Plasma Fields', 'Aurora Seven', 'Titan\'s Gate', 'Cosmic Drift',
    'Starfall', 'Binary Ghost', 'Echo Prime', 'Meteor Storm',
    'Galactic Core', 'Wormhole Alpha', 'Solar Flare', 'Ice Giant',
    'Thunder Bay', 'Lightning Strike', 'Frozen Hell', 'Burning Sky',
    'Crystal Storm', 'Acid Rain', 'Nuclear Winter', 'Toxic Waste',
    'Paradise Lost', 'Heaven\'s Gate', 'Devil\'s Playground', 'Angel\'s Tears',
    'Dragon\'s Breath', 'Phoenix Rising', 'Serpent\'s Coil', 'Eagle\'s Nest',
    'Wolf\'s Den', 'Bear\'s Cave', 'Tiger\'s Leap', 'Shark\'s Fin',
    'Ocean Deep', 'Mountain High', 'Desert Storm', 'Forest Green',
    'River Wild', 'Lake Calm', 'Valley Low', 'Hill Top'
]

# Görev örnekleri
SAMPLE_MISSIONS = [
    {
        'name': 'Merkez koloni su arıtma sistemi',
        'description': 'Temel madde tedariki',
        'requirements': {'Oksijen': 350},
        'reward': 15000
    },
    {
        'name': 'Orion gemi onarım tesisi',
        'description': 'Yapı malzemesi tedariki',
        'requirements': {'Çelik': 400, 'İridyum': 50, 'Tungsten': 25},
        'reward': 25000
    },
    {
        'name': 'Ay üssü alfa savunma sistemi',
        'description': 'Ana madde tedariği',
        'requirements': {'Kayber Kristal': 500},
        'reward': 30000
    },
    {
        'name': 'Galaktik enerji reaktörü',
        'description': 'Quantum teknoloji geliştirme',
        'requirements': {'Quantum Çekirdeği': 100, 'Zephyrium': 200},
        'reward': 40000
    },
    {
        'name': 'Stardust araştırma projesi',
        'description': 'Uzay keşif teknolojisi',
        'requirements': {'Stardust': 300, 'Silisyum': 150, 'Mavi Kristal': 75},
        'reward': 35000
    },
    {
        'name': 'Titan kolonisi kurulumu',
        'description': 'Yeni yerleşim bölgesi inşaatı',
        'requirements': {'Çelik': 600, 'Demir': 400, 'Oksijen': 250},
        'reward': 45000
    },
    {
        'name': 'Quantum sıçrama kapısı',
        'description': 'Hızlı yolculuk teknolojisi',
        'requirements': {'Quantum Çekirdeği': 200, 'Kayber Kristal': 300, 'Zephyrium': 150},
        'reward': 55000
    },
    {
        'name': 'Nebula madencilik operasyonu',
        'description': 'Nadir mineral çıkarımı',
        'requirements': {'İridyum': 100, 'Tungsten': 200, 'U-235': 50},
        'reward': 38000
    }
]
