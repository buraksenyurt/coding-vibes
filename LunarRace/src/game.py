# Lunar Race - Ana Oyun Sınıfı
import pygame
import random
import time
from constants import *
from actors import *
from utils import *

class Game:
    """Ana oyun sınıfı"""
    def __init__(self):
        pygame.init()
        self.screen = pygame.display.set_mode((SCREEN_WIDTH, SCREEN_HEIGHT))
        pygame.display.set_caption("Lunar Race")
        self.clock = pygame.time.Clock()
        self.running = True
        
        # Oyun durumu
        self.state = "MENU"  # MENU, GAME, MISSION_SELECT, TRADE, GAME_OVER
        self.spaceron = INITIAL_SPACERON
        self.inventory = {}  # Mineral envanteri
        self.current_mission = None
        self.current_planet = None
        self.distance_to_planet = 0
        
        # Aktörler
        self.ship = Ship(100, (TOP_BAR_HEIGHT + SCREEN_HEIGHT - BOTTOM_BAR_HEIGHT) // 2)
        self.asteroids = []
        self.blackholes = []
        self.repair_ships = []
        self.planets = []
        self.comets = []
        self.trade_stations = []
        
        # Zamanlamalar
        self.last_asteroid_spawn = 0
        self.last_blackhole_spawn = 0
        self.last_repair_spawn = 0
        self.last_planet_spawn = 0
        self.last_comet_spawn = 0
        self.last_trade_spawn = 0
        
        # Görevler
        self.available_missions = SAMPLE_MISSIONS.copy()
        self.selected_mission_index = 0
        self.mission_scroll_offset = 0  # Kaydırma offset'i
        self.missions_per_page = 4  # Sayfa başına görev sayısı
        
        # UI Fontları
        self.font_small = pygame.font.Font(None, 20)
        self.font_medium = pygame.font.Font(None, 28)
        self.font_large = pygame.font.Font(None, 36)
        
    def handle_events(self):
        """Olayları işle"""
        for event in pygame.event.get():
            if event.type == pygame.QUIT:
                self.running = False
            elif event.type == pygame.KEYDOWN:
                if self.state == "MENU":
                    if event.key == pygame.K_RETURN:
                        self.state = "MISSION_SELECT"
                    elif event.key == pygame.K_ESCAPE:
                        self.running = False
                elif self.state == "MISSION_SELECT":
                    if event.key == pygame.K_UP:
                        self.selected_mission_index = (self.selected_mission_index - 1) % len(self.available_missions)
                        # Kaydırma kontrolü - yukarı
                        if self.selected_mission_index < self.mission_scroll_offset:
                            self.mission_scroll_offset = self.selected_mission_index
                    elif event.key == pygame.K_DOWN:
                        self.selected_mission_index = (self.selected_mission_index + 1) % len(self.available_missions)
                        # Kaydırma kontrolü - aşağı
                        if self.selected_mission_index >= self.mission_scroll_offset + self.missions_per_page:
                            self.mission_scroll_offset = self.selected_mission_index - self.missions_per_page + 1
                    elif event.key == pygame.K_RETURN:
                        self.current_mission = self.available_missions[self.selected_mission_index]
                        self.state = "GAME"
                        self.reset_game()
                    elif event.key == pygame.K_ESCAPE:
                        self.state = "MENU"
                elif self.state == "GAME":
                    if event.key == pygame.K_ESCAPE:
                        self.state = "MENU"
                elif self.state == "GAME_OVER":
                    if event.key == pygame.K_RETURN:
                        self.state = "MENU"
                        self.reset_game()
                    elif event.key == pygame.K_ESCAPE:
                        self.running = False
                        
    def reset_game(self):
        """Oyunu sıfırla"""
        self.ship = Ship(100, (TOP_BAR_HEIGHT + SCREEN_HEIGHT - BOTTOM_BAR_HEIGHT) // 2)
        self.spaceron = INITIAL_SPACERON
        self.inventory = {}
        self.asteroids.clear()
        self.blackholes.clear()
        self.repair_ships.clear()
        self.planets.clear()
        self.comets.clear()
        self.trade_stations.clear()
        # Mission scroll'u sıfırla
        self.mission_scroll_offset = 0
        
    def spawn_actors(self, dt):
        """Aktörleri oluştur"""
        current_time = time.time()
        
        # Göktaşı oluştur
        if (current_time - self.last_asteroid_spawn > random.uniform(2, 5) and 
            len(self.asteroids) < MAX_ASTEROIDS):
            y = random.randint(TOP_BAR_HEIGHT + 20, SCREEN_HEIGHT - BOTTOM_BAR_HEIGHT - 20)
            self.asteroids.append(Asteroid(SCREEN_WIDTH + 50, y))
            self.last_asteroid_spawn = current_time
            
        # Kara delik oluştur (nadir)
        if (current_time - self.last_blackhole_spawn > random.uniform(45, 90) and 
            len(self.blackholes) == 0):
            x = random.randint(SCREEN_WIDTH // 3, SCREEN_WIDTH - 100)
            y = random.randint(TOP_BAR_HEIGHT + 50, SCREEN_HEIGHT - BOTTOM_BAR_HEIGHT - 50)
            self.blackholes.append(BlackHole(x, y))
            self.last_blackhole_spawn = current_time
            
        # Onarım gemisi oluştur
        if (current_time - self.last_repair_spawn > random.uniform(20, 40) and 
            len(self.repair_ships) < MAX_REPAIR_SHIPS):
            y = random.randint(TOP_BAR_HEIGHT + 20, SCREEN_HEIGHT - BOTTOM_BAR_HEIGHT - 20)
            self.repair_ships.append(RepairShip(SCREEN_WIDTH + 50, y))
            self.last_repair_spawn = current_time
            
        # Gezegen oluştur
        if (current_time - self.last_planet_spawn > random.uniform(8, 15) and 
            len(self.planets) < MAX_PLANETS):
            y = random.randint(TOP_BAR_HEIGHT + 50, SCREEN_HEIGHT - BOTTOM_BAR_HEIGHT - 50)
            self.planets.append(Planet(SCREEN_WIDTH + 100, y))
            self.last_planet_spawn = current_time
            
        # Kuyruklu yıldız oluştur (çok nadir)
        if (current_time - self.last_comet_spawn > random.uniform(60, 120) and 
            len(self.comets) == 0):
            self.comets.append(Comet())
            self.last_comet_spawn = current_time
            
        # Takas üssü oluştur
        if (current_time - self.last_trade_spawn > random.uniform(30, 60) and 
            len(self.trade_stations) == 0):
            y = random.randint(TOP_BAR_HEIGHT + 20, SCREEN_HEIGHT - BOTTOM_BAR_HEIGHT - 20)
            self.trade_stations.append(TradeStation(SCREEN_WIDTH + 50, y))
            self.last_trade_spawn = current_time
            
    def update_actors(self, dt):
        """Aktörleri güncelle"""
        keys = pygame.key.get_pressed()
        self.ship.update(keys, dt)
        
        # Göktaşları güncelle
        for asteroid in self.asteroids[:]:
            asteroid.update(dt)
            if asteroid.is_off_screen():
                self.asteroids.remove(asteroid)
                
        # Kara delikleri güncelle
        for blackhole in self.blackholes[:]:
            blackhole.update(dt)
            if blackhole.is_expired():
                self.blackholes.remove(blackhole)
                
        # Onarım gemilerini güncelle
        for repair_ship in self.repair_ships[:]:
            repair_ship.update(dt)
            if repair_ship.is_off_screen():
                self.repair_ships.remove(repair_ship)
                
        # Gezegenleri güncelle
        for planet in self.planets[:]:
            planet.update(dt)
            if planet.is_off_screen():
                self.planets.remove(planet)
                
        # Kuyruklu yıldızları güncelle
        for comet in self.comets[:]:
            comet.update(dt)
            if comet.is_off_screen():
                self.comets.remove(comet)
                
        # Takas üsslerini güncelle
        for station in self.trade_stations[:]:
            station.update(dt)
            if station.is_off_screen():
                self.trade_stations.remove(station)
                
    def check_collisions(self):
        """Çarpışmaları kontrol et"""
        # Göktaşı çarpışmaları
        for asteroid in self.asteroids[:]:
            if check_collision(self.ship.rect, asteroid.rect):
                self.ship.take_damage(ASTEROID_DAMAGE)
                self.asteroids.remove(asteroid)
                
        # Kara delik çekimi
        for blackhole in self.blackholes[:]:
            if blackhole.pull_ship(self.ship):
                self.blackholes.remove(blackhole)
                
        # Onarım gemisi çarpışmaları
        for repair_ship in self.repair_ships[:]:
            if check_collision(self.ship.rect, repair_ship.rect) and not repair_ship.used:
                # Repair kutusunu kullan
                self.ship.repair(repair_ship.repair_amount)
                self.ship.celluron += repair_ship.fuel_amount
                repair_ship.used = True  # Kullanıldı olarak işaretle, ama kaldırma
                
        # Kuyruklu yıldız çarpışmaları
        for comet in self.comets[:]:
            if check_collision(self.ship.rect, comet.rect):
                self.ship.take_damage(COMET_DAMAGE)
                self.state = "GAME_OVER"
                
    def update_game(self, dt):
        """Oyunu güncelle"""
        if self.state == "GAME":
            self.spawn_actors(dt)
            self.update_actors(dt)
            self.check_collisions()
            
            # Oyun bitti mi kontrol et
            if self.ship.damage >= 100 or self.ship.celluron <= 0:
                self.state = "GAME_OVER"
                
    def draw_menu(self):
        """Ana menüyü çiz"""
        self.screen.fill(BLACK)
        
        # Başlık
        title_text = self.font_large.render("LUNAR RACE", True, WHITE)
        title_rect = title_text.get_rect(center=(SCREEN_WIDTH//2, 100))
        self.screen.blit(title_text, title_rect)
        
        # Alt başlık
        subtitle_text = self.font_medium.render("Uzayın Sonsuz Karanlığında Yolculuk", True, GRAY)
        subtitle_rect = subtitle_text.get_rect(center=(SCREEN_WIDTH//2, 140))
        self.screen.blit(subtitle_text, subtitle_rect)
        
        # Açıklama
        desc_lines = [
            "Yıldızlar arasında seyahat eden bir gezgin olarak,",
            "kuyruklu yıldızlar, kara delikler ve göktaşları arasından",
            "sıyrılarak gezegenlerden değerli mineraller toplayın.",
            "",
            "ENTER - Görev Seçimi",
            "ESC - Çıkış"
        ]
        
        y = 180
        for line in desc_lines:
            if line:
                text = self.font_small.render(line, True, WHITE)
                text_rect = text.get_rect(center=(SCREEN_WIDTH//2, y))
                self.screen.blit(text, text_rect)
            y += 25
            
    def draw_mission_select(self):
        """Görev seçim ekranını çiz"""
        self.screen.fill(BLACK)
        
        # Başlık
        title_text = self.font_large.render("GÖREV SEÇİMİ", True, WHITE)
        title_rect = title_text.get_rect(center=(SCREEN_WIDTH//2, 50))
        self.screen.blit(title_text, title_rect)
        
        # Sayfa bilgisi
        total_pages = (len(self.available_missions) + self.missions_per_page - 1) // self.missions_per_page
        current_page = (self.mission_scroll_offset // self.missions_per_page) + 1
        page_text = f"Sayfa {current_page}/{total_pages} ({len(self.available_missions)} görev)"
        page_surface = self.font_small.render(page_text, True, GRAY)
        page_rect = page_surface.get_rect(center=(SCREEN_WIDTH//2, 75))
        self.screen.blit(page_surface, page_rect)
        
        # Görevleri sadece görünür olanları çiz
        y = 100
        mission_height = 85  # Her görev için yükseklik
        
        visible_missions = self.available_missions[self.mission_scroll_offset:self.mission_scroll_offset + self.missions_per_page]
        
        for i, mission in enumerate(visible_missions):
            actual_index = self.mission_scroll_offset + i
            color = YELLOW if actual_index == self.selected_mission_index else WHITE
            
            # Seçili görev için arka plan vurgusu
            if actual_index == self.selected_mission_index:
                pygame.draw.rect(self.screen, (40, 40, 40), (30, y - 5, SCREEN_WIDTH - 60, mission_height - 5), 0)
                pygame.draw.rect(self.screen, YELLOW, (30, y - 5, SCREEN_WIDTH - 60, mission_height - 5), 2)
            
            # Görev numarası ve adı
            mission_number = f"{actual_index + 1}."
            number_surface = self.font_medium.render(mission_number, True, color)
            self.screen.blit(number_surface, (40, y))
            
            name_text = self.font_medium.render(mission['name'], True, color)
            self.screen.blit(name_text, (70, y))
            
            # Açıklama
            desc_text = self.font_small.render(mission['description'], True, GRAY)
            self.screen.blit(desc_text, (70, y + 22))
            
            # Gereksinimler (kısaltılmış)
            req_parts = []
            for mineral, amount in mission['requirements'].items():
                req_parts.append(f"{amount} {mineral[:10]}")
            req_text = "Gereksinimler: " + ", ".join(req_parts)
            
            # Uzun metni kes
            if len(req_text) > 80:
                req_text = req_text[:77] + "..."
            
            req_surface = self.font_small.render(req_text, True, color)
            self.screen.blit(req_surface, (70, y + 42))
            
            # Ödül
            reward_text = f"Ödül: {format_number(mission['reward'])} Spaceron"
            reward_surface = self.font_small.render(reward_text, True, GREEN)
            self.screen.blit(reward_surface, (70, y + 62))
            
            y += mission_height
        
        # Kaydırma işaretleri
        if self.mission_scroll_offset > 0:
            # Yukarı ok
            up_text = "▲ Daha fazla görev yukarıda"
            up_surface = self.font_small.render(up_text, True, CYAN)
            up_rect = up_surface.get_rect(center=(SCREEN_WIDTH//2, 95))
            self.screen.blit(up_surface, up_rect)
        
        if self.mission_scroll_offset + self.missions_per_page < len(self.available_missions):
            # Aşağı ok
            down_text = "▼ Daha fazla görev aşağıda"
            down_surface = self.font_small.render(down_text, True, CYAN)
            down_rect = down_surface.get_rect(center=(SCREEN_WIDTH//2, y + 10))
            self.screen.blit(down_surface, down_rect)
        
        # Kontroller
        control_text = "YÖN TUŞLARI - Gezin, ENTER - Başla, ESC - Geri"
        control_surface = self.font_small.render(control_text, True, WHITE)
        control_rect = control_surface.get_rect(center=(SCREEN_WIDTH//2, SCREEN_HEIGHT - 30))
        self.screen.blit(control_surface, control_rect)
        
    def draw_game(self):
        """Oyun ekranını çiz"""
        self.screen.fill(BLACK)
        
        # Aktörleri çiz
        for planet in self.planets:
            planet.draw(self.screen)
            
        for asteroid in self.asteroids:
            asteroid.draw(self.screen)
            
        for blackhole in self.blackholes:
            blackhole.draw(self.screen)
            
        for repair_ship in self.repair_ships:
            repair_ship.draw(self.screen)
            
        for comet in self.comets:
            comet.draw(self.screen)
            
        for station in self.trade_stations:
            station.draw(self.screen)
            
        self.ship.draw(self.screen)
        
        # UI çiz
        self.draw_ui()
        
    def draw_ui(self):
        """Kullanıcı arayüzünü çiz"""
        # Üst bar (büyütülmüş)
        pygame.draw.rect(self.screen, GRAY, (0, 0, SCREEN_WIDTH, TOP_BAR_HEIGHT), 2)
        
        # Üst bar - Sol taraf
        # Bütçe
        budget_text = f"Spaceron: {format_number(int(self.spaceron))}"
        budget_surface = self.font_small.render(budget_text, True, WHITE)
        self.screen.blit(budget_surface, (10, 10))
        
        # Görev
        if self.current_mission:
            mission_text = f"Görev: {self.current_mission['name'][:25]}..."
            mission_surface = self.font_small.render(mission_text, True, WHITE)
            self.screen.blit(mission_surface, (10, 30))
            
        # Üst bar - Sağ taraf: Gezegen bilgileri
        if self.planets:
            planet_info_x = SCREEN_WIDTH - 450  # Daha fazla alan
            planet_count = 0
            visible_planets = [planet for planet in self.planets if planet.x > 0 and planet.x < SCREEN_WIDTH]
            
            for i, planet in enumerate(visible_planets[:2]):  # En fazla 2 gezegen göster
                # Her gezegen için farklı x pozisyonu
                current_x = planet_info_x + (i * 225)  # Yan yana yerleştir
                
                # Gezegen adı (kısaltılmış)
                info_text = f"P{i+1}: {planet.data['name'][:10]}"
                info_surface = self.font_small.render(info_text, True, BLUE)
                self.screen.blit(info_surface, (current_x, 10))
                
                # Mineralleri kısaltılmış şekilde göster
                mineral_texts = []
                for mineral, data in list(planet.data['minerals'].items())[:2]:  # Sadece ilk 2 mineral
                    mineral_texts.append(f"{mineral[:6]}: {data['doka']}D")
                
                if mineral_texts:
                    mineral_text = " | ".join(mineral_texts)
                    if len(planet.data['minerals']) > 2:
                        mineral_text += "..."
                    
                    # Uzun metni kes
                    if len(mineral_text) > 20:
                        mineral_text = mineral_text[:17] + "..."
                        
                    mineral_surface = self.font_small.render(mineral_text, True, CYAN)
                    self.screen.blit(mineral_surface, (current_x, 30))
            
            # Eğer 2'den fazla gezegen varsa göstergesi
            if len(visible_planets) > 2:
                more_text = f"+{len(visible_planets) - 2} daha"
                more_surface = self.font_small.render(more_text, True, GRAY)
                self.screen.blit(more_surface, (SCREEN_WIDTH - 80, 45))
            
        # Alt bar (büyütülmüş)
        pygame.draw.rect(self.screen, GRAY, (0, SCREEN_HEIGHT - BOTTOM_BAR_HEIGHT, SCREEN_WIDTH, BOTTOM_BAR_HEIGHT), 2)
        
        # Alt bar - İlk satır
        # Hız
        speed_text = f"Hız: {self.ship.speed:.1f}"
        speed_surface = self.font_small.render(speed_text, True, WHITE)
        self.screen.blit(speed_surface, (10, SCREEN_HEIGHT - 45))
        
        # Yakıt
        fuel_color = GREEN if self.ship.celluron > 300 else YELLOW if self.ship.celluron > 100 else RED
        fuel_text = f"Celluron: {int(self.ship.celluron)}"
        fuel_surface = self.font_small.render(fuel_text, True, fuel_color)
        self.screen.blit(fuel_surface, (120, SCREEN_HEIGHT - 45))
        
        # Hasar
        if self.ship.damage > 0:
            damage_color = YELLOW if self.ship.damage < 50 else RED
            damage_text = f"Hasar: %{int(self.ship.damage)}"
            damage_surface = self.font_small.render(damage_text, True, damage_color)
            self.screen.blit(damage_surface, (250, SCREEN_HEIGHT - 45))
            
        # Alt bar - İkinci satır
        # WARP durumu
        if self.ship.warp_active:
            warp_text = f"WARP: {self.ship.warp_time:.1f}s"
            warp_surface = self.font_small.render(warp_text, True, CYAN)
            self.screen.blit(warp_surface, (10, SCREEN_HEIGHT - 25))
        elif self.ship.warp_cooldown > 0:
            cooldown_text = f"WARP Cooldown: {self.ship.warp_cooldown:.1f}s"
            cooldown_surface = self.font_small.render(cooldown_text, True, GRAY)
            self.screen.blit(cooldown_surface, (10, SCREEN_HEIGHT - 25))
        else:
            ready_text = "WARP: Hazır (Space tuşu)"
            ready_surface = self.font_small.render(ready_text, True, GREEN)
            self.screen.blit(ready_surface, (10, SCREEN_HEIGHT - 25))
                    
    def draw_game_over(self):
        """Oyun bitti ekranını çiz"""
        self.screen.fill(BLACK)
        
        # Başlık
        title_text = self.font_large.render("OYUN BİTTİ", True, RED)
        title_rect = title_text.get_rect(center=(SCREEN_WIDTH//2, 150))
        self.screen.blit(title_text, title_rect)
        
        # Sebep
        if self.ship.damage >= 100:
            reason_text = "Geminiz kuyruklu yıldız tarafından imha edildi!"
        elif self.ship.celluron <= 0:
            reason_text = "Yakıtınız tükendi!"
        else:
            reason_text = "Geminiz hasar aldı!"
            
        reason_surface = self.font_medium.render(reason_text, True, WHITE)
        reason_rect = reason_surface.get_rect(center=(SCREEN_WIDTH//2, 200))
        self.screen.blit(reason_surface, reason_rect)
        
        # İstatistikler
        stats_y = 250
        stats = [
            f"Kalan Spaceron: {format_number(int(self.spaceron))}",
            f"Kalan Celluron: {int(max(0, self.ship.celluron))}",
            f"Toplam Hasar: %{int(self.ship.damage)}"
        ]
        
        for stat in stats:
            stat_surface = self.font_small.render(stat, True, GRAY)
            stat_rect = stat_surface.get_rect(center=(SCREEN_WIDTH//2, stats_y))
            self.screen.blit(stat_surface, stat_rect)
            stats_y += 25
            
        # Kontroller
        control_text = "ENTER - Tekrar Oyna, ESC - Çıkış"
        control_surface = self.font_small.render(control_text, True, WHITE)
        control_rect = control_surface.get_rect(center=(SCREEN_WIDTH//2, SCREEN_HEIGHT - 50))
        self.screen.blit(control_surface, control_rect)
        
    def draw(self):
        """Çizim yöneticisi"""
        if self.state == "MENU":
            self.draw_menu()
        elif self.state == "MISSION_SELECT":
            self.draw_mission_select()
        elif self.state == "GAME":
            self.draw_game()
        elif self.state == "GAME_OVER":
            self.draw_game_over()
            
        pygame.display.flip()
        
    def run(self):
        """Ana oyun döngüsü"""
        while self.running:
            dt = self.clock.tick(FPS) / 1000.0  # Delta time in seconds
            
            self.handle_events()
            self.update_game(dt)
            self.draw()
            
        pygame.quit()
