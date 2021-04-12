export class Hud {

    constructor(i18next) {
        this.i18next = i18next;
        this.gasBars = 0;
    }

    updateMoney(amount) {
        this.money = parseInt(amount);

        document.getElementById('hand-money').textContent = this.i18next.t('hud.money', { amount: this.money });
    }

    toggleVehicleWarning(warningClass) {
        let panel = document.getElementById('warnings');

        let warning = panel.querySelector('.vehicle-icon.' + warningClass);

        if (!warning) {
            warning = document.createElement('div');
            warning.className = 'vehicle-icon';

            warning.classList.add(warningClass);

            panel.appendChild(warning);
        } else {
            panel.removeChild(warning);
        }
    }

    updateVehicleGas(gas) {
        let currentBars = Math.round(gas * 0.2);

        if (currentBars !== this.gasBars) {
            this.gasBars = parseInt(currentBars);

            this.updateGasCounter();
        }
    }

    updateGasCounter() {
        let bars = document.getElementsByClassName('gas-counter')[0].children;

        for (let i = 0; i < bars.length; i++) {

            if (i < this.gasBars) {
                bars[i].classList.add('filled');
            } else {
                bars[i].classList.remove('filled');
            }
        }
    }
}
