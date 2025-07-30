# Lunar Race - Aktör Sınıfları
import pygame
import math
import random
from constants import *
from utils import *

class Ship:
    """Oyuncunun uzay mekiği"""
    def __init__(self, x, y):
        self.x = x
        self.y = y
        self.speed = SHIP_NORMAL_SPEED
        self.celluron = INITIAL_CELLURON
        self.damage = 0  # Yüzde hasar
        self.warp_active = False
        self.warp_time = 0
        self.warp_cooldown = 0
        self.fuel_efficiency = 1.0  # Kara delik hasarından sonra artar
        self.rect = pygame.Rect(x-10, y-10, 20, 20)
        
    def update(self, keys, dt):
        """Mekiği güncelle"""
        # Hareket kontrolü
        dx, dy = 0, 0
        
        if keys[KEYS['UP']] or keys[KEYS['UP_ARROW']]:
            dy = -self.speed
        if keys[KEYS['DOWN']] or keys[KEYS['DOWN_ARROW']]:
            dy = self.speed
        if keys[KEYS['LEFT']] or keys[KEYS['LEFT_ARROW']]:
            dx = -self.speed
        if keys[KEYS['RIGHT']] or keys[KEYS['RIGHT_ARROW']]:
            dx = self.speed
            
        # WARP kontrolü
        if keys[KEYS['WARP']] and not self.warp_active and self.warp_cooldown <= 0:
            self.warp_active = True
            self.warp_time = WARP_DURATION
            self.speed = SHIP_WARP_SPEED
            
        # WARP zamanı güncelle
        if self.warp_active:
            self.warp_time -= dt
            if self.warp_time <= 0:
                self.warp_active = False
                self.warp_cooldown = WARP_COOLDOWN
                self.speed = SHIP_NORMAL_SPEED if self.damage < 50 else SHIP_DAMAGED_SPEED
                
        # WARP cooldown güncelle
        if self.warp_cooldown > 0:
            self.warp_cooldown -= dt
            
        # Pozisyon güncelle
        self.x += dx
        self.y += dy
        
        # Ekran sınırları (UI barlarını dikkate al)
        self.x = clamp(self.x, 10, SCREEN_WIDTH - 10)
        self.y = clamp(self.y, TOP_BAR_HEIGHT + 10, SCREEN_HEIGHT - BOTTOM_BAR_HEIGHT - 10)
        
        # Rect güncelle
        self.rect.center = (self.x, self.y)
        
        # Yakıt tüketimi (her 5 saniyede 1 birim)
        # dt saniye geçtiyse, 5 saniyede 1 birim için dt/5 oranında azalt
        fuel_decrease = (dt / 5.0) * self.fuel_efficiency
        self.celluron -= fuel_decrease
        
    def draw(self, surface):
        """Mekiği çiz (üçgen vektör)"""
        # Ana üçgen
        points = [
            (self.x + 15, self.y),      # Burun
            (self.x - 10, self.y - 8),  # Sol kanat
            (self.x - 10, self.y + 8)   # Sağ kanat
        ]
        
        color = GREEN if self.damage < 25 else YELLOW if self.damage < 75 else RED
        draw_vector_polygon(surface, color, points, 3)
        
        # WARP efekti
        if self.warp_active:
            for i in range(3):
                offset = (i + 1) * 5
                warp_points = [
                    (self.x + 15 - offset, self.y),
                    (self.x - 10 - offset, self.y - 8),
                    (self.x - 10 - offset, self.y + 8)
                ]
                alpha_color = (*CYAN[:3], max(0, 255 - i * 80))
                draw_vector_polygon(surface, CYAN, warp_points, 1)
                
    def take_damage(self, damage_amount):
        """Hasar al"""
        if damage_amount == COMET_DAMAGE:  # Kuyruklu yıldız
            self.damage = 100
        elif damage_amount == ASTEROID_DAMAGE:  # Göktaşı
            # Göktaşı hem hasar verir hem celluron azaltır
            self.damage += damage_amount  # %15 hasar
            self.celluron -= 50  # 50 celluron kaybı
        else:  # Kara delik (yüzde hasar)
            self.damage += damage_amount
            if self.damage >= 50:
                self.speed = SHIP_DAMAGED_SPEED
                self.fuel_efficiency = 1.1
                
    def repair(self, repair_amount):
        """Onarım yap"""
        self.damage = max(0, self.damage - repair_amount)
        if self.damage < 50:
            self.speed = SHIP_NORMAL_SPEED
            self.fuel_efficiency = 1.0

class Asteroid:
    """Göktaşı aktörü"""
    def __init__(self, x, y):
        self.x = x
        self.y = y
        self.speed = random.uniform(ASTEROID_SPEED_MIN, ASTEROID_SPEED_MAX)
        self.sides = random.choice([3, 4, 6, 8, 16])
        self.size = random.randint(10, 30)
        self.rotation = 0
        self.rotation_speed = random.uniform(-2, 2)
        self.points = generate_random_polygon((0, 0), self.sides, self.size//2, self.size)
        self.rect = pygame.Rect(x - self.size, y - self.size, self.size*2, self.size*2)
        
    def update(self, dt):
        """Göktaşını güncelle"""
        self.x -= self.speed
        self.rotation += self.rotation_speed * dt
        self.rect.center = (self.x, self.y)
        
    def draw(self, surface):
        """Göktaşını çiz"""
        # Rotasyon uygula
        rotated_points = []
        cos_r = math.cos(self.rotation)
        sin_r = math.sin(self.rotation)
        
        for px, py in self.points:
            rx = px * cos_r - py * sin_r + self.x
            ry = px * sin_r + py * cos_r + self.y
            rotated_points.append((rx, ry))
            
        draw_vector_polygon(surface, WHITE, rotated_points, 2)
        
    def is_off_screen(self):
        """Ekran dışında mı kontrol et"""
        return self.x < -self.size

class BlackHole:
    """Kara delik aktörü"""
    def __init__(self, x, y):
        self.x = x
        self.y = y
        self.radius = random.randint(30, 60)
        self.pull_radius = self.radius * 3
        self.rect = pygame.Rect(x - self.radius, y - self.radius, self.radius*2, self.radius*2)
        self.lifetime = random.uniform(5, 10)  # Saniye
        
    def update(self, dt):
        """Kara deliği güncelle"""
        self.lifetime -= dt
        
    def draw(self, surface):
        """Kara deliği çiz"""
        # Çekim alanı
        draw_vector_circle(surface, GRAY, (int(self.x), int(self.y)), self.pull_radius, 1)
        # Ana kara delik
        draw_vector_circle(surface, BLACK, (int(self.x), int(self.y)), self.radius, 3)
        # İç halka
        draw_vector_circle(surface, RED, (int(self.x), int(self.y)), self.radius//2, 2)
        
    def pull_ship(self, ship):
        """Gemiyi çek"""
        distance = calculate_distance((self.x, self.y), (ship.x, ship.y))
        if distance < self.pull_radius:
            # Çekim kuvveti uygula
            pull_strength = 1 - (distance / self.pull_radius)
            dx = (self.x - ship.x) * pull_strength * 0.1
            dy = (self.y - ship.y) * pull_strength * 0.1
            ship.x += dx
            ship.y += dy
            
            # İçine düştü mü?
            if distance < self.radius:
                ship.take_damage(BLACKHOLE_DAMAGE)
                return True
        return False
        
    def is_expired(self):
        """Süresi doldu mu"""
        return self.lifetime <= 0

class RepairShip:
    """Onarım gemisi"""
    def __init__(self, x, y):
        self.x = x
        self.y = y
        self.speed = REPAIR_SHIP_SPEED
        self.width = 60
        self.height = 20
        self.repair_amount = random.choice([5, 10, 20])
        self.fuel_amount = random.choice([10, 20, 50])
        self.rect = pygame.Rect(x - self.width//2, y - self.height//2, self.width, self.height)
        self.used = False  # Repair kutusunun kullanılıp kullanılmadığını takip eder
        
    def update(self, dt):
        """Onarım gemisini güncelle"""
        self.x -= self.speed
        self.rect.center = (self.x, self.y)
        
    def draw(self, surface):
        """Onarım gemisini çiz"""
        # Ana dikdörtgen
        rect_points = [
            (self.x - self.width//2, self.y - self.height//2),
            (self.x + self.width//2, self.y - self.height//2),
            (self.x + self.width//2, self.y + self.height//2),
            (self.x - self.width//2, self.y + self.height//2)
        ]
        
        # Kullanılmışsa gri, kullanılmamışsa yeşil
        color = GRAY if self.used else GREEN
        draw_vector_polygon(surface, color, rect_points, 2)
        
        # "Repair" yazısı
        text = "Used" if self.used else "Repair"
        draw_text(surface, text, (self.x - 25, self.y - 8), WHITE, 16)
        
    def is_off_screen(self):
        """Ekran dışında mı"""
        return self.x < -self.width

class Planet:
    """Gezegen aktörü (arka plan)"""
    def __init__(self, x, y):
        self.x = x
        self.y = y
        self.radius = random.randint(40, 80)
        self.speed = random.uniform(0.5, 1.5)
        self.data = generate_planet_data()
        self.rect = pygame.Rect(x - self.radius, y - self.radius, self.radius*2, self.radius*2)
        
    def update(self, dt):
        """Gezegeni güncelle"""
        self.x -= self.speed
        self.rect.center = (self.x, self.y)
        
    def draw(self, surface):
        """Gezegeni çiz"""
        # Ana daire (dış çember)
        draw_vector_circle(surface, BLUE, (int(self.x), int(self.y)), self.radius, 2)
        
        # Çoklu iç çemberler (daha belirgin görünüm için)
        for i in range(1, 6):  # 5 farklı çember
            inner_radius = self.radius * (0.8 - i * 0.15)  # Giderek küçülen çemberler
            if inner_radius > 3:  # Çok küçük çemberler çizme
                alpha = max(50, 255 - i * 40)  # Giderek solan renkler
                color = CYAN if i % 2 == 0 else BLUE
                draw_vector_circle(surface, color, (int(self.x), int(self.y)), int(inner_radius), 1)
        
        # Merkez nokta
        draw_vector_circle(surface, WHITE, (int(self.x), int(self.y)), 2, 0)
        
    def is_off_screen(self):
        """Ekran dışında mı"""
        return self.x < -self.radius

class Comet:
    """Kuyruklu yıldız"""
    def __init__(self):
        self.x = SCREEN_WIDTH + 50
        self.y = random.randint(TOP_BAR_HEIGHT + 20, (TOP_BAR_HEIGHT + SCREEN_HEIGHT - BOTTOM_BAR_HEIGHT)//2)
        self.target_y = random.randint((TOP_BAR_HEIGHT + SCREEN_HEIGHT - BOTTOM_BAR_HEIGHT)//2, SCREEN_HEIGHT - BOTTOM_BAR_HEIGHT - 20)
        self.speed = COMET_SPEED
        self.head_radius = 8
        self.tail_segments = []
        self.rect = pygame.Rect(self.x - 10, self.y - 10, 20, 20)
        
        # Kuyruk segmentleri oluştur
        for i in range(15):
            segment_x = self.x + i * 10
            segment_y = self.y + random.uniform(-5, 5)
            self.tail_segments.append((segment_x, segment_y))
            
    def update(self, dt):
        """Kuyruklu yıldızı güncelle"""
        # Eğri hareket
        progress = (SCREEN_WIDTH + 50 - self.x) / (SCREEN_WIDTH + 100)
        self.y = lerp(self.y, self.target_y, progress * 0.02)
        
        self.x -= self.speed
        
        # Kuyruk segmentlerini güncelle
        for i in range(len(self.tail_segments) - 1, 0, -1):
            self.tail_segments[i] = self.tail_segments[i-1]
        if self.tail_segments:
            self.tail_segments[0] = (self.x + 15, self.y + random.uniform(-2, 2))
            
        self.rect.center = (self.x, self.y)
        
    def draw(self, surface):
        """Kuyruklu yıldızı çiz"""
        # Kuyruk çiz
        if len(self.tail_segments) > 1:
            for i in range(len(self.tail_segments) - 1):
                alpha = 255 - (i * 15)
                if alpha > 0:
                    draw_vector_line(surface, YELLOW, self.tail_segments[i], self.tail_segments[i+1], 2)
        
        # Baş çiz
        draw_vector_circle(surface, WHITE, (int(self.x), int(self.y)), self.head_radius, 2)
        
    def is_off_screen(self):
        """Ekran dışında mı"""
        return self.x < -50

class TradeStation:
    """Takas üssü"""
    def __init__(self, x, y):
        self.x = x
        self.y = y
        self.speed = TRADE_STATION_SPEED
        self.width = 100
        self.height = 40
        self.rect = pygame.Rect(x - self.width//2, y - self.height//2, self.width, self.height)
        
    def update(self, dt):
        """Takas üssünü güncelle"""
        self.x -= self.speed
        self.rect.center = (self.x, self.y)
        
    def draw(self, surface):
        """Takas üssünü çiz"""
        # Ana dikdörtgen
        rect_points = [
            (self.x - self.width//2, self.y - self.height//2),
            (self.x + self.width//2, self.y - self.height//2),
            (self.x + self.width//2, self.y + self.height//2),
            (self.x - self.width//2, self.y + self.height//2)
        ]
        draw_vector_polygon(surface, ORANGE, rect_points, 3)
        
        # "Takas Üssü" yazısı
        draw_text(surface, "Takas Ussu", (self.x - 40, self.y - 8), WHITE, 16)
        
    def is_off_screen(self):
        """Ekran dışında mı"""
        return self.x < -self.width
