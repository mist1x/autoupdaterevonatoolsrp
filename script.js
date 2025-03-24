// Calendar Component
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
    }

    init() {
        if (!this.checkElements()) return;
        this.renderCalendar();
        this.addNavigation();
    }

    checkElements() {
        return !!document.getElementById('calendarBody') && 
               !!document.querySelector('.calendar-header');
    }

    renderCalendar() {
        try {
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
                    week += `<td class="${classes.join(' ')}">${day}</td>`;
                }
                week += '</tr>';
                calendarBody += week;
                if (dayCounter + (i + 1) * 7 > daysInMonth) break;
            }

            document.getElementById('calendarBody').innerHTML = calendarBody;
        } catch (error) {
            console.error('Calendar render error:', error);
        }
    }

    addNavigation() {
        const navHTML = `
            <button class="prev-month">‹</button>
            <button class="next-month">›</button>
        `;
        
        const navContainer = document.createElement('div');
        navContainer.className = 'calendar-nav';
        navContainer.innerHTML = navHTML;
        
        document.querySelector('.calendar-header').appendChild(navContainer);
        
        navContainer.querySelector('.prev-month').addEventListener('click', () => {
            this.currentDate.setMonth(this.currentDate.getMonth() - 1);
            this.renderCalendar();
        });
        
        navContainer.querySelector('.next-month').addEventListener('click', () => {
            this.currentDate.setMonth(this.currentDate.getMonth() + 1);
            this.renderCalendar();
        });
    }
}

// Swiper Initialization
function initSwiper() {
    try {
        const bannerContainer = document.querySelector('.bannerContainer');
        if (!bannerContainer) return;

        bannerContainer.innerHTML = `
            <div class="swiper">
                <div class="swiper-wrapper">
                    ${['banner_x2', 'banner_intro', 'banner_bonuses', 'banner_moders']
                        .map(img => `
                            <div class="swiper-slide">
                                <img src="https://cdn.rustage.su/aboba/img/${img}.png">
                            </div>
                        `).join('')}
                </div>
                <div class="swiper-pagination"></div>
                <div class="swiper-button-prev"></div>
                <div class="swiper-button-next"></div>
            </div>
        `;

        new Swiper('.swiper', {
            loop: true,
            autoplay: { delay: 7000 },
            pagination: { el: '.swiper-pagination', clickable: true },
            navigation: {
                nextEl: '.swiper-button-next',
                prevEl: '.swiper-button-prev',
            }
        });
    } catch (error) {
        console.error('Swiper init error:', error);
    }
}

// UI Enhancements
function enhanceUI() {
    const injectSocials = () => {
        try {
            const socialsHTML = `
                <div class="socials">
                    ${Object.entries({
                        vk: window.vk_link,
                        telegram: window.telegram_link,
                        discord: window.discord_link
                    }).map(([network, url]) => `
                        <a href="${url}" target="_blank" class="socials-${network}">
                            <img src="https://gspics.org/images/2025/02/04/IVD${{
                                vk: 'EZD',
                                telegram: 'hcL',
                                discord: 'tgy'
                            }[network]}.png">
                        </a>
                    `).join('')}
                </div>
            `;
            
            const target = document.querySelector('.PlayerMenu-module__langSwitcher');
            if (target) target.insertAdjacentHTML('beforeend', socialsHTML);
        } catch (error) {
            console.error('Socials injection error:', error);
        }
    };

    const injectIcons = () => {
        try {
            const icons = {
                '.HeaderNav-module__link[href="/"]': 'IVDBPO',
                '.PlayerBalance-module__btn': 'wallet.png',
                '.SupportLink-module__link': 'IVDs4i'
            };

            Object.entries(icons).forEach(([selector, icon]) => {
                const element = document.querySelector(selector);
                if (element) {
                    element.insertAdjacentHTML('beforeend', `
                        <img class="nav-icon" src="${icon.startsWith('http') 
                            ? icon 
                            : 'https://gspics.org/images/2025/02/04/' + icon}">
                    `);
                }
            });
        } catch (error) {
            console.error('Icons injection error:', error);
        }
    };

    injectSocials();
    injectIcons();
}

// Main Controller
function initApp() {
    window.componentsManager.addListener('WIDGETS', 'DID_MOUNT', () => {
        setTimeout(() => {
            try {
                new Calendar().init();
                initSwiper();
            } catch (error) {
                console.error('Components init error:', error);
            }
        }, 1500);
    });

    window.componentsManager.load();
    enhanceUI();
}

// Entry Point
if (window.isAppReady) {
    initApp();
} else {
    window.addEventListener('appReady', initApp);
}
