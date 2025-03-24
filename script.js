class Calendar {
    constructor() {
        this.currentDate = new Date();
        this.weekDays = window.widget_wipecalendar 
            ? window.widget_wipecalendar.split(',').map(Number).filter(d => d >= 0 && d <= 6)
            : [];
        this.monthNames = [
            'Январь', 'Февраль', 'Март', 'Апрель', 'Май', 'Июнь',
            'Июль', 'Август', 'Сентябрь', 'Октябрь', 'Ноябрь', 'Декабрь'
        ];
        this.config = {
            animationDuration: 300,
            maxEventCount: 5
        };
    }

    init() {
        this.loadDependencies()
            .then(() => this.setupEventListeners())
            .then(() => this.renderCalendar())
            .catch(error => console.error('Initialization error:', error));
    }

    loadDependencies() {
        return new Promise((resolve, reject) => {
            if (typeof Swiper === 'undefined') {
                const script = document.createElement('script');
                script.src = 'https://unpkg.com/swiper/swiper-bundle.min.js';
                script.onload = resolve;
                script.onerror = reject;
                document.head.appendChild(script);
            } else {
                resolve();
            }
        });
    }

    setupEventListeners() {
        window.addEventListener('resize', () => this.debounce(this.renderCalendar, 150));
        document.addEventListener('click', e => this.handleDocumentClick(e));
    }

    renderCalendar() {
        const year = this.currentDate.getFullYear();
        const month = this.currentDate.getMonth();
        
        document.getElementById('monthName').textContent = this.monthNames[month];
        document.getElementById('year').textContent = year;

        const firstDay = new Date(year, month, 1);
        const lastDay = new Date(year, month + 1, 0);
        const startDay = firstDay.getDay() === 0 ? 6 : firstDay.getDay() - 1;
        const daysInMonth = lastDay.getDate();

        let calendarBody = '';
        let dayCounter = 1 - startDay;

        for (let i = 0; i < 6; i++) {
            let week = '<tr>';
            
            for (let j = 0; j < 7; j++) {
                const currentDay = dayCounter + j + i * 7;
                const isCurrentMonth = currentDay > 0 && currentDay <= daysInMonth;
                const day = isCurrentMonth ? currentDay : '';
                
                let classes = [];
                if (isCurrentMonth) {
                    const date = new Date(year, month, currentDay);
                    
                    if (date.toDateString() === new Date().toDateString()) {
                        classes.push('today');
                    }
                    
                    if (this.weekDays.includes(date.getDay())) {
                        classes.push('wipeday');
                    }
                }

                week += `<td class="${classes.join(' ')}" ${day ? `data-day="${day}"` : ''}>${day}</td>`;
            }
            
            week += '</tr>';
            calendarBody += week;
            
            if (dayCounter + (i + 1) * 7 > daysInMonth) break;
        }

        document.getElementById('calendarBody').innerHTML = calendarBody;
    }

    addNavigation() {
        const nav = document.createElement('div');
        nav.className = 'calendar-nav';
        nav.innerHTML = `
            <button class="prev-month">‹</button>
            <button class="next-month">›</button>
        `;
        
        document.querySelector('.calendar-header').appendChild(nav);
        
        nav.querySelector('.prev-month').addEventListener('click', () => {
            this.currentDate.setMonth(this.currentDate.getMonth() - 1);
            this.renderCalendar();
        });
        
        nav.querySelector('.next-month').addEventListener('click', () => {
            this.currentDate.setMonth(this.currentDate.getMonth() + 1);
            this.renderCalendar();
        });
    }
}

    debounce(func, delay) {
        let timer;
        return (...args) => {
            clearTimeout(timer);
            timer = setTimeout(() => func.apply(this, args), delay);
        };
    }
}

class ComponentManager {
    constructor() {
        this.components = {};
        this.retryInterval = 100;
        this.maxRetries = 10;
    }

    registerComponent(name, checkFn, initFn) {
        this.components[name] = {
            checkFn,
            initFn,
            attempts: 0
        };
    }

    start() {
        Object.entries(this.components).forEach(([name, component]) => {
            const interval = setInterval(() => {
                if (component.checkFn()) {
                    clearInterval(interval);
                    component.initFn();
                } else if (component.attempts++ > this.maxRetries) {
                    clearInterval(interval);
                    console.error(`Component ${name} failed to load`);
                }
            }, this.retryInterval);
        });
    }
}

function initSwiper() {
    
}

function main() {
    const componentManager = new ComponentManager();
    
    componentManager.registerComponent('HEADER', 
        () => !!document.querySelector('.PlayerMenu-module__langSwitcher'),
        () => {
            // Логика инициализации хедера
        }
    );

    componentManager.registerComponent('WIDGETS', 
        () => !!document.querySelector('.widget-container'),
        () => {
            new Calendar().init();
            initSwiper();
        }
    );

    componentManager.start();

    // Дополнительная логика
    document.addEventListener('visibilitychange', () => {
        if (document.visibilityState === 'visible') {
            window.dispatchEvent(new CustomEvent('appResume'));
        }
    });
}

const initInterval = setInterval(() => {
    if (window.isAppReady) {
        clearInterval(initInterval);
        main();
    }
}, 50);

setTimeout(() => {
    if (!window.isAppReady) {
        console.warn('Fallback initialization');
        main();
    }
}, 5000);
