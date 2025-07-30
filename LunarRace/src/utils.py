# Lunar Race - Yardımcı Fonksiyonlar
import pygame
import math
import random
from constants import *

def draw_vector_line(surface, color, start_pos, end_pos, width=2):
    """Vektör çizgi çizer"""
    pygame.draw.line(surface, color, start_pos, end_pos, width)

def draw_vector_polygon(surface, color, points, width=2):
    """Vektör poligon çizer"""
    if len(points) > 2:
        pygame.draw.polygon(surface, color, points, width)

def draw_vector_circle(surface, color, center, radius, width=2):
    """Vektör daire çizer"""
    pygame.draw.circle(surface, color, center, radius, width)

def draw_text(surface, text, pos, color=WHITE, size=24):
    """Metin çizer"""
    font = pygame.font.Font(None, size)
    text_surface = font.render(text, True, color)
    surface.blit(text_surface, pos)
    return text_surface.get_rect(topleft=pos)

def calculate_distance(pos1, pos2):
    """İki nokta arasındaki mesafeyi hesaplar"""
    return math.sqrt((pos1[0] - pos2[0])**2 + (pos1[1] - pos2[1])**2)

def check_collision(rect1, rect2):
    """İki dikdörtgen arasında çarpışma kontrolü"""
    return rect1.colliderect(rect2)

def generate_random_polygon(center, sides, min_radius, max_radius):
    """Rastgele poligon noktaları oluşturur"""
    points = []
    angle_step = 2 * math.pi / sides
    
    for i in range(sides):
        angle = i * angle_step + random.uniform(-0.2, 0.2)
        radius = random.uniform(min_radius, max_radius)
        x = center[0] + radius * math.cos(angle)
        y = center[1] + radius * math.sin(angle)
        points.append((x, y))
    
    return points

def clamp(value, min_value, max_value):
    """Değeri belirli aralıkta tutar"""
    return max(min_value, min(value, max_value))

def lerp(start, end, t):
    """Linear interpolation"""
    return start + (end - start) * t

def generate_planet_data():
    """Rastgele gezegen verisi oluşturur"""
    name = random.choice(PLANET_NAMES)
    minerals = {}
    
    # Her gezegen en fazla 3 mineral barındırır
    selected_minerals = random.sample(MINERALS, random.randint(1, 3))
    
    for mineral in selected_minerals:
        doka_amount = random.randint(10, 100)
        price_per_unit = random.randint(50, 5000)
        minerals[mineral] = {
            'doka': doka_amount,
            'price': price_per_unit
        }
    
    distance = random.randint(100, 10000)
    
    return {
        'name': name,
        'minerals': minerals,
        'distance': distance
    }

def format_number(num):
    """Sayıları okunabilir formatta gösterir"""
    if num >= 1000:
        return f"{num:,}"
    return str(num)
