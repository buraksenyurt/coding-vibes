# Lunar Race - Ana Program
"""
Lunar Race - 2D Platform Uzay Oyunu

Bu oyun, Game Design Document'e göre geliştirilmiş bir 2D platform oyunudur.
Oyuncu bir uzay mekiği kullanarak yıldızlar arasında yolculuk eder,
göktaşları, kara delikler ve kuyruklu yıldızlardan kaçar,
değerli mineraller toplar ve görevleri tamamlar.

Gereksinimler:
- Python 3.7+
- Pygame

Çalıştırma:
python main.py

Kontroller:
- W/A/S/D veya Yön tuşları: Mekik hareketi
- Space: WARP hızı (5 saniye, 60 saniye cooldown)
- Enter: Menülerde seçim
- ESC: Geri/Çıkış
"""

import sys
import os

# Pygame'i kontrol et
try:
    import pygame
except ImportError:
    print("Pygame kütüphanesi bulunamadı!")
    print("Lütfen pygame'i yükleyin: pip install pygame")
    sys.exit(1)

# Proje dosyalarını import et
try:
    from game import Game
except ImportError as e:
    print(f"Proje dosyaları yüklenemedi: {e}")
    print("Lütfen tüm dosyaların src/ klasöründe olduğundan emin olun.")
    sys.exit(1)

def main():
    """Ana fonksiyon"""
    print("Lunar Race başlatılıyor...")
    print("=" * 50)
    print("KONTROLLER:")
    print("W/A/S/D veya Yön tuşları - Mekik hareketi")
    print("Space - WARP hızı (5 saniye süre, 60 saniye cooldown)")
    print("Enter - Menülerde seçim")
    print("ESC - Geri/Çıkış")
    print("=" * 50)
    
    try:
        # Oyunu başlat
        game = Game()
        game.run()
        
    except Exception as e:
        print(f"Oyun hatası: {e}")
        import traceback
        traceback.print_exc()
    
    print("Lunar Race kapatıldı. İyi yolculuklar!")

if __name__ == "__main__":
    main()
