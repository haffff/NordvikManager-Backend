# Nordvik Manager backend (Previously named DNDOnePlaceManager)

Its backend part of Nordvik Manager VTT application. For release please go to [main repository](https://github.com/haffff/NordvikManager)

## How to run
### Backend:
1. Change JWTSecret in AppSettings.Development.json
2. If you want to use Postgresql DB. Change "UseSQLite" and "ConntectionStrings" fields.
3. Run DNDOnePlaceManager project

### Frontend
Frontend repository can be found [here](https://github.com/haffff/NordvikManagerFrontEnd). You can run it in development mode( `npm start` ) or create build( `npm run build` ) and put it in `wwwroot` directory.

Rest of documentation in progress
