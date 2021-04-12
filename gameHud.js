import { Hud } from '../model/hud.class.js';

let hud = null;
let startingKms = 0.0;

mp.events.add('initializeHudValues', (voiceRange, money) => {

    loadModule('hud', 'gameHud', 'wp-main-hud').then(() => {
        hud = new Hud(i18next);

        // na ekran
        hud.updateMoney(money);

        // html
        translateDataElements();
    });
});

mp.events.add('updateVoiceIcon', (range) => {

});

mp.events.add('updatePlayerMoney', (money) => {

    hud.updateMoney(money);
});

mp.events.add('showSpeedometer', (model, speed, gas, distance) => {
    document.getElementById('speedometer').classList.remove('no-display');

    document.getElementById('vehicle-name').textContent = model;

    hud.toggleVehicleWarning('seatbelt');

    updateSpeedometerValues(speed, gas, distance);
});

mp.events.add('toggleVehicleWarning', (warning) => {
    hud.toggleVehicleWarning(warning);
});

mp.events.add('updateSpeedometer', (speed, gas, distance, inTaxiRoute) => {
    updateSpeedometerValues(speed, gas, distance);

    if (inTaxiRoute) {
        updateTaxiDistanceFare(distance);
    }
});

mp.events.add('hideSpeedometer', () => {
    document.getElementById('warnings').innerHTML = '';

    document.getElementById('speedometer').classList.add('no-display');
});

mp.events.add('initializeTaxiMeter', (license, tariff, kms) => {
    startingKms = parseFloat(kms).toFixed(1);

    document.getElementById('taxi-license').textContent = i18next.t('hud.taxi-license', { license: license });

    document.getElementById('taxi-amount').textContent = i18next.t('hud.money', { amount: 0 });
    document.getElementById('taxi-distance').textContent = '0.0';
    document.getElementById('taxi-tariff').textContent = tariff;

    document.getElementById('taximeter').classList.remove('no-display');
});

mp.events.add('updateTaxiMeter', (kms) => {
    updateTaxiDistanceFare(kms);
});

mp.events.add('hideTaxiMeter', () => {
    document.getElementById('taximeter').classList.add('no-display');
});

function updateSpeedometerValues(speed, gas, distance) {
    document.getElementById('vehicle-speed').textContent = speed;

    document.getElementById('vehicle-distance').textContent = (distance / 1000).toFixed(1);

    hud.updateVehicleGas(gas);
}

function updateTaxiDistanceFare(kms) {
    let distance = parseFloat(kms / 1000 - startingKms).toFixed(1);
    let money = Math.round(distance * 50);

    document.getElementById('taxi-amount').textContent = i18next.t('hud.money', { amount: money });

    document.getElementById('taxi-distance').textContent = distance;
}
