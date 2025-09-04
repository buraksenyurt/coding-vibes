package main

import (
	"fmt"
	"image"
	_ "image/png"
	"log"
	"math"
	"math/rand"
	"os"
	"path/filepath"
	"time"

	"github.com/hajimehoshi/ebiten/v2"
	"github.com/hajimehoshi/ebiten/v2/ebitenutil"
)

const (
	screenWidth  = 900
	screenHeight = 400
	tankSpeed    = 2.0
	rotationSpeed = 0.05
	damageRadius = 50  // Adjusted for 0.25 scale (was 50 * 0.25)
	maxMines     = 5
	maxBonuses   = 10
	maxObstacles = 4
)

type Game struct {
	tank           *Tank
	mines          []*Mine
	bonuses        []*Bonus
	obstacles      []*Obstacle
	score          int
	armor          int
	ground         *ebiten.Image
	mineImg        *ebiten.Image
	bonusImg       *ebiten.Image
	obstacleImg    *ebiten.Image
	explosionImgs  []*ebiten.Image
	groundTile1    *ebiten.Image
	groundTile2    *ebiten.Image
	moving         bool
}

type Tank struct {
	x, y         float64
	angle        float64
	hullImg      *ebiten.Image
	gunImg       *ebiten.Image
	trackImg     *ebiten.Image
	exhaustImg   *ebiten.Image
}

type Mine struct {
	x, y           float64
	timer          time.Time
	exploding      bool
	explosionFrame int
	frameCounter   int  // New: counter to slow down animation
	img            *ebiten.Image
}

type Bonus struct {
	x, y float64
	img  *ebiten.Image
}

type Obstacle struct {
	x, y float64
	img  *ebiten.Image
}

func (g *Game) Update() error {
	g.moving = false
	// Handle input
	if ebiten.IsKeyPressed(ebiten.KeyArrowLeft) {
		g.tank.angle += rotationSpeed
	}
	if ebiten.IsKeyPressed(ebiten.KeyArrowRight) {
		g.tank.angle -= rotationSpeed
	}
	if ebiten.IsKeyPressed(ebiten.KeySpace) {
		g.moving = true
		// Move forward (corrected direction - 90 degrees counterclockwise)
		g.tank.x += math.Cos(g.tank.angle - math.Pi/2) * tankSpeed
		g.tank.y += math.Sin(g.tank.angle - math.Pi/2) * tankSpeed
		// Check bounds
		if g.tank.x < 0 { g.tank.x = 0 }
		if g.tank.x > screenWidth { g.tank.x = screenWidth }
		if g.tank.y < 0 { g.tank.y = 0 }
		if g.tank.y > screenHeight { g.tank.y = screenHeight }
		// Check obstacles
		for _, obs := range g.obstacles {
			if distance(g.tank.x, g.tank.y, obs.x, obs.y) < 7.5 {  // Adjusted for 0.25 scale
				// Revert move (corrected direction - 90 degrees counterclockwise)
				g.tank.x -= math.Cos(g.tank.angle - math.Pi/2) * tankSpeed
				g.tank.y -= math.Sin(g.tank.angle - math.Pi/2) * tankSpeed
			}
		}
	}

	// Update mines
	now := time.Now()
	for i, mine := range g.mines {
		if !mine.exploding && now.After(mine.timer) {
			mine.exploding = true
			mine.explosionFrame = 0
			mine.frameCounter = 0  // Initialize frame counter
			// Damage tank immediately when mine explodes
			if distance(g.tank.x, g.tank.y, mine.x, mine.y) < damageRadius {
				g.armor -= 10
			}
		}
		if mine.exploding {
			mine.frameCounter++
			if mine.frameCounter >= 8 {  // Advance frame every 8 game frames (slows down animation)
				mine.frameCounter = 0
				mine.explosionFrame++
			}
			if mine.explosionFrame >= len(g.explosionImgs) {
				// Remove mine
				g.mines = append(g.mines[:i], g.mines[i+1:]...)
				// Add new mine
				g.addMine()
				break
			}
		}
	}

	// Check bonuses
	for i, bonus := range g.bonuses {
		if distance(g.tank.x, g.tank.y, bonus.x, bonus.y) < 30 { 
			g.score++
			g.bonuses = append(g.bonuses[:i], g.bonuses[i+1:]...)
			g.addBonus()
			break
		}
	}

	// Game over
	if g.armor <= 0 {
		// Reset or something
		g.reset()
	}

	return nil
}

func (g *Game) Draw(screen *ebiten.Image) {
	scale := 0.25  // Reduced from 0.5 to 0.25 for smaller assets
	// Draw ground
	screen.DrawImage(g.ground, nil)

	// Draw obstacles
	for _, obs := range g.obstacles {
		op := &ebiten.DrawImageOptions{}
		op.GeoM.Scale(scale, scale)
		op.GeoM.Translate(obs.x, obs.y)
		screen.DrawImage(obs.img, op)
	}

	// Draw bonuses
	for _, bonus := range g.bonuses {
		op := &ebiten.DrawImageOptions{}
		op.GeoM.Scale(scale, scale)
		op.GeoM.Translate(bonus.x, bonus.y)
		screen.DrawImage(bonus.img, op)
	}

	// Draw mines
	for _, mine := range g.mines {
		if mine.exploding {
			if mine.explosionFrame < len(g.explosionImgs) {
				op := &ebiten.DrawImageOptions{}
				op.GeoM.Scale(scale, scale)
				op.GeoM.Translate(mine.x, mine.y)
				screen.DrawImage(g.explosionImgs[mine.explosionFrame], op)
			}
		} else {
			op := &ebiten.DrawImageOptions{}
			op.GeoM.Scale(scale, scale)
			op.GeoM.Translate(mine.x, mine.y)
			screen.DrawImage(g.mineImg, op)
		}
	}

	// Draw tank
	g.drawTank(screen)

	// Draw UI
	ebitenutil.DebugPrint(screen, fmt.Sprintf("Score: %d Armor: %d", g.score, g.armor))
}

func (g *Game) drawTank(screen *ebiten.Image) {
	scale := 0.25  // Reduced from 0.5 to 0.25 for smaller tank
	// Draw hull
	op := &ebiten.DrawImageOptions{}
	op.GeoM.Translate(-float64(g.tank.hullImg.Bounds().Dx())/2, -float64(g.tank.hullImg.Bounds().Dy())/2)
	op.GeoM.Scale(scale, scale)
	op.GeoM.Rotate(g.tank.angle)
	op.GeoM.Translate(g.tank.x, g.tank.y)
	screen.DrawImage(g.tank.hullImg, op)

	// Draw gun
	op = &ebiten.DrawImageOptions{}
	op.GeoM.Translate(-float64(g.tank.gunImg.Bounds().Dx())/2, -float64(g.tank.gunImg.Bounds().Dy())/2)
	op.GeoM.Scale(scale, scale)
	op.GeoM.Rotate(g.tank.angle)
	op.GeoM.Translate(g.tank.x, g.tank.y)
	screen.DrawImage(g.tank.gunImg, op)

	// Draw tracks
	op = &ebiten.DrawImageOptions{}
	op.GeoM.Translate(-float64(g.tank.trackImg.Bounds().Dx())/2, -float64(g.tank.trackImg.Bounds().Dy())/2)
	op.GeoM.Scale(scale, scale)
	op.GeoM.Rotate(g.tank.angle)
	op.GeoM.Translate(g.tank.x, g.tank.y)
	screen.DrawImage(g.tank.trackImg, op)

	// Draw exhaust if moving
	if g.moving {
		op = &ebiten.DrawImageOptions{}
		op.GeoM.Translate(-float64(g.tank.exhaustImg.Bounds().Dx())/2, -float64(g.tank.exhaustImg.Bounds().Dy())/2)
		op.GeoM.Scale(scale, scale)
		op.GeoM.Rotate(g.tank.angle)
		op.GeoM.Translate(g.tank.x - math.Cos(g.tank.angle - math.Pi/2)*5, g.tank.y - math.Sin(g.tank.angle - math.Pi/2)*5)
		screen.DrawImage(g.tank.exhaustImg, op)
	}
}

func (g *Game) Layout(outsideWidth, outsideHeight int) (int, int) {
	return screenWidth, screenHeight
}

func (g *Game) addMine() {
	if len(g.mines) >= maxMines {
		return
	}
	x := rand.Float64() * screenWidth
	y := rand.Float64() * screenHeight
	// Ensure not overlapping with other mines
	for _, m := range g.mines {
		if distance(x, y, m.x, m.y) < 12.5 {  // Adjusted for 0.25 scale
			return
		}
	}
	// Ensure not overlapping with obstacles or tank
	for _, obs := range g.obstacles {
		if distance(x, y, obs.x, obs.y) < 12.5 {  // Adjusted for 0.25 scale
			return
		}
	}
	if distance(x, y, g.tank.x, g.tank.y) < 12.5 {  // Adjusted for 0.25 scale
		return
	}
	timer := time.Now().Add(time.Duration(10+rand.Intn(20)) * time.Second)
	mine := &Mine{x: x, y: y, timer: timer, img: g.mineImg}
	g.mines = append(g.mines, mine)
}

func (g *Game) addBonus() {
	x := rand.Float64() * screenWidth
	y := rand.Float64() * screenHeight
	// Ensure not overlapping with obstacles
	for _, obs := range g.obstacles {
		if distance(x, y, obs.x, obs.y) < 7.5 {  // Adjusted for 0.25 scale
			return
		}
	}
	bonus := &Bonus{x: x, y: y, img: g.bonusImg}
	g.bonuses = append(g.bonuses, bonus)
}

func (g *Game) reset() {
	g.tank = &Tank{x: 50, y: screenHeight - 50, angle: 0, hullImg: g.tank.hullImg, gunImg: g.tank.gunImg, trackImg: g.tank.trackImg, exhaustImg: g.tank.exhaustImg}
	g.mines = []*Mine{}
	g.bonuses = []*Bonus{}
	g.score = 0
	g.armor = 100
	for i := 0; i < maxBonuses; i++ {
		g.addBonus()
	}
	for i := 0; i < maxMines; i++ {
		g.addMine()
	}
	g.obstacles = []*Obstacle{}
	for i := 0; i < maxObstacles; i++ {
		x := rand.Float64() * screenWidth
		y := rand.Float64() * screenHeight
		obs := &Obstacle{x: x, y: y, img: g.obstacleImg}
		g.obstacles = append(g.obstacles, obs)
	}
}

func distance(x1, y1, x2, y2 float64) float64 {
	dx := x1 - x2
	dy := y1 - y2
	return math.Sqrt(dx*dx + dy*dy)
}

func loadImage(path string) *ebiten.Image {
	f, err := os.Open(path)
	if err != nil {
		log.Fatal(err)
	}
	defer f.Close()
	img, _, err := image.Decode(f)
	if err != nil {
		log.Fatal(err)
	}
	return ebiten.NewImageFromImage(img)
}

func main() {
	rand.Seed(time.Now().UnixNano())

	game := &Game{}

	// Load images
	assetsDir := filepath.Join("..", "assets")
	game.tank = &Tank{}
	game.tank.hullImg = loadImage(filepath.Join(assetsDir, "tank", "Hull_01.png"))
	game.tank.gunImg = loadImage(filepath.Join(assetsDir, "tank", "Gun_01.png"))
	game.tank.trackImg = loadImage(filepath.Join(assetsDir, "tank", "Track_1_A.png"))
	game.tank.exhaustImg = loadImage(filepath.Join(assetsDir, "tank", "Exhaust_Fire.png"))

	game.mineImg = loadImage(filepath.Join(assetsDir, "mine", "Mine.png"))
	game.bonusImg = loadImage(filepath.Join(assetsDir, "bonus", "Coin_A.png"))
	game.obstacleImg = loadImage(filepath.Join(assetsDir, "decor", "Czech_Hdgehog_A.png"))

	game.explosionImgs = []*ebiten.Image{}
	for i := 1; i <= 7; i++ {
		path := fmt.Sprintf(filepath.Join(assetsDir, "mine", "Mine_Explosion_A_%03d.png"), i)
		game.explosionImgs = append(game.explosionImgs, loadImage(path))
	}

	game.groundTile1 = loadImage(filepath.Join(assetsDir, "ground", "Ground_Tile_01_C.png"))
	game.groundTile2 = loadImage(filepath.Join(assetsDir, "ground", "Ground_Tile_02_C.png"))

	// Create ground
	game.ground = ebiten.NewImage(screenWidth, screenHeight)
	tileW := game.groundTile1.Bounds().Dx()
	tileH := game.groundTile1.Bounds().Dy()
	for x := 0; x < screenWidth; x += tileW {
		for y := 0; y < screenHeight; y += tileH {
			var tile *ebiten.Image
			if rand.Intn(2) == 0 {
				tile = game.groundTile1
			} else {
				tile = game.groundTile2
			}
			op := &ebiten.DrawImageOptions{}
			op.GeoM.Translate(float64(x), float64(y))
			game.ground.DrawImage(tile, op)
		}
	}

	game.reset()

	ebiten.SetWindowSize(screenWidth, screenHeight)
	ebiten.SetWindowTitle("Walkure")
	if err := ebiten.RunGame(game); err != nil {
		log.Fatal(err)
	}
}
