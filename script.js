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
        this.renderCalendar();
        this.addNavigation();
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

function initSwiper() {
    
    try {
        const bannerContainer = document.querySelector('.container.bannerContainer')
        
        if (!bannerContainer) {
            return;
        }

        const originalContent = bannerContainer.innerHTML;

        bannerContainer.innerHTML = '';

        const swiperHTML = `
            <swiper-container 
                class="mySwiper"
                pagination="true"
                pagination-clickable="true"
                navigation="true"
                space-between="30"
                centered-slides="true"
                autoplay-delay="7000"
                autoplay-disable-on-interaction="false"
            >
                <swiper-slide style="width: 1240px; margin-right: 30px;">
                    <img src="https://cdn.rustage.su/aboba/img/banner_x2.png">
                </swiper-slide>
                <swiper-slide style="width: 1240px; margin-right: 30px;">
                    <img src="https://cdn.rustage.su/aboba/img/banner_intro.png">
                </swiper-slide>
                <swiper-slide style="width: 1240px; margin-right: 30px;">
                    <img src="https://cdn.rustage.su/aboba/img/banner_bonuses.png">
                </swiper-slide>
                <swiper-slide style="width: 1240px; margin-right: 30px;">
                    <img src="https://cdn.rustage.su/aboba/img/banner_moders.png">
                </swiper-slide>
            </swiper-container>
        `;

        bannerContainer.innerHTML = swiperHTML;

        const swiperElement = bannerContainer.querySelector('swiper-container');

        if (!swiperElement) {
            return;
        }

        const swiper = new Swiper(swiperElement, {
            loop: true,
            autoplay: {
                delay: 7000,
                disableOnInteraction: false,
            },
			navigation: {
              nextEl: '.swiper-button-next',
              prevEl: '.swiper-button-prev',
            }
        });

    } catch (error) {
        if (error instanceof TypeError) {
        }
    } finally {
        console.groupEnd();
    }
}

function main() {
    window.dispatchEvent(new CustomEvent("setCustomConfig"));
    window.dispatchEvent(new CustomEvent("initState"));
    window.dispatchEvent(new CustomEvent("initComponentsManager"));
  	
	
    window.componentsManager.addListener('HEADER', 'DID_MOUNT', () => {
      
      	const { player } = window.getState().player

        
      	const socials = `
        <div class="socials">
          <a href="${window.vk_link}" target="_blank" class="socials-vk" title="Наш ВКонтакте">
            <img class="socials-icon" src="https://gspics.org/images/2025/02/04/IVDEZD.png">
          </a>
          <a href="${window.telegram_link}" target="_blank" class="socials-telegram" title="Наш Telegram канал">
            <img class="socials-icon" src="https://gspics.org/images/2025/02/04/IVDhcL.png">
          </a>
          <a href="${window.discord_link}" target="_blank" class="socials-discord" title="Наш Discord">
            <img class="socials-icon" src="https://gspics.org/images/2025/02/04/IVDtgy.png">
          </a>
        </div>
        `
        
        const shopIcon = `
			<img class="nav-icon" src="https://gspics.org/images/2025/02/04/IVDBPO.png">
        `
        
        const supIcon = `
			<img class="nav-icon" src="https://gspics.org/images/2025/02/04/IVDs4i.png">
		`
        
        const balanceIcon = `
			<img class="nav-icon" src="https://cdn.rustage.su/aboba/img/wallet.png">
		`
        
        const langBtn = document.querySelector('.PlayerMenu-module__langSwitcher')
		langBtn.insertAdjacentHTML('beforeend', socials)

		const shopBtn = document.querySelector('.HeaderNav-module__link[href="/"]')
		shopBtn.insertAdjacentHTML('beforeend', shopIcon)
      	
		const balanceBtn = document.querySelector('.PlayerBalance-module__btn')
		balanceBtn.insertAdjacentHTML('beforeend', balanceIcon)

		const supBtn = document.querySelector('.SupportLink-module__link')
		supBtn.insertAdjacentHTML('beforeend', supIcon)
    });


    window.componentsManager.addListener('WIDGETS', 'DID_MOUNT', () => {
        setTimeout(() => {
            try {
                new Calendar().init();
				initSwiper();
            } catch (error) {
                console.error('Calendar init error:', error);
            }
        }, 1500);
    });

    window.componentsManager.load();

    window.copyConnect = function(element) {
        navigator.clipboard.writeText(window.server_connect_1)
            .then(() => {
                element.textContent = 'Скопировано!';
                setTimeout(() => {
                    element.textContent = 'Скопировать коннект';
                }, 2000);
            })
            .catch(err => {
                console.error('Copy failed:', err);
            });
    };
}

if(window.isAppReady) {
  main();
} else {
    window.addEventListener('appReady', () => {
    main();
  })
}
