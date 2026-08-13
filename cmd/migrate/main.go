package main

import (
	"log"

	"github.com/Mightyfin/erp/internal/config"
	"github.com/Mightyfin/erp/internal/database"
)

func main() {
	cfg, err := config.Load()
	if err != nil {
		log.Fatal(err)
	}
	if err := database.Migrate(cfg.DatabaseURL); err != nil {
		log.Fatal(err)
	}
}
