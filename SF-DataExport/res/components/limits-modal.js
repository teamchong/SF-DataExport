Vue.component('limits-modal', {
    template,
    data() {
        return {
            chart: null,
            chartWidth: parseInt(Math.max(700, window.innerWidth * 0.9)),
            chartHeight: parseInt(Math.max(400, window.innerHeight - 400)),
            chartType: 'm',
            chartTypes: {
                m: 'minute',
                h: 'hour',
                d: 'day',
                w: 'week',
                M: 'month',
                Q: 'quarter',
                y: 'year'
            },
            hidden: {},
            tab: 'index',
            timeFormat: 'YYYY-MM-DD HH:mm:ss'
        };
    },
    mounted() {
        this.$nextTick(function () {
            this.renderChart(this.orgLimitsLog, this.chartType);
        });
    },
    watch: {
        tab() {
            this.renderChart(this.orgLimitsLog);
        },
        hidden(value) {
            this.renderChart(this.orgLimitsLog);
        },
        chartType(value) {
            this.renderChart(this.orgLimitsLog);
        },
        orgLimitsLog(value) {
            this.renderChart(value);
        }
    },
    methods: {
        renderChart(data) {
            const { chart } = this.$refs;
            if (this.tab === 'logging' && chart) {
                if (!this.chart) {
                    this.chart = new Chart(chart.getContext('2d'), {
                        type: 'line',
                        data,
                        options: {
                            title: {
                                text: 'Org limits'
                            },
                            legend: {
                                position: 'bottom',
                                onClick: (_e, { text }) => {
                                    this.$nextTick(function () {
                                        if (this.hidden[text]) {
                                            this.hidden = _.omit(this.hidden, [text]);
                                        } else {
                                            this.hidden = Object.assign({ [text]: true }, this.hidden);
                                        }
                                    });
                                }
                            },
                            scales: {
                                xAxes: [{
                                    type: 'time',
                                    time: {
                                        parser: date => {
                                            return moment.utc(date, this.timeFormat).local();
                                        },
                                        tooltipFormat: this.timeFormat
                                    },
                                    scaleLabel: {
                                        display: true,
                                        labelString: 'Date'
                                    }
                                }],
                                yAxes: [{
                                    ticks: {
                                        min: 0,
                                        max: 100,
                                        callback: function (value) {
                                            return value + '%'; // convert it to percentage
                                        }
                                    },
                                    scaleLabel: {
                                        display: true,
                                        labelString: '% of usage'
                                    }
                                }]
                            }
                        }
                    });
                } else {
                    this.chart.data = data;
                    this.chart.update();
                }
            }
        },
        newDate(value) {
            return moment().add(-value, this.chartType).toDate();
        },
        newDateString(value) {
            return moment().add(-value, this.chartType).format(this.timeFormat);
        },
        switchTab(value) {
            this.tab = value;
        },
        showAll() {
            if (this.chart) {
                this.hidden = {};
            }
        },
        hideAll() {
            if (this.chart) {
                const hidden = {};
                const { datasets } = this.chart.data;
                const len = datasets ? datasets.length : 0;
                for (let i = 0; i < len; i++) {
                    const { label } = datasets[i];
                    hidden[label] = true;
                }
                this.hidden = hidden;
            }
        }
    },
    computed: {
        cmdLimits() {
            const { cmd, currentInstanceUrl } = this.$store.state;
            return cmd + ' loglimits@' + this.$options.filters.orglabel(currentInstanceUrl);
        },
        currentInstanceUrl() {
            return this.$store.state.currentInstanceUrl;
        },
        orgLimits() {
            return this.$store.state.orgLimits;
        },
        orgLimitsLog() {
            const { orgLimitsLog } = this.$store.state;
            const dataLookup = {};
            const colors = ['#e6194b', '#3cb44b', '#ffe119', '#4363d8', '#f58231', '#911eb4', '#46f0f0', '#f032e6', '#bcf60c', '#fabebe', '#008080', '#e6beff', '#9a6324', '#fffac8', '#800000', '#aaffc3', '#808000', '#ffd8b1', '#000075', '#808080', '#ffffff', '#000000'];
            const { color } = Chart.helpers;
            const len = orgLimitsLog ? orgLimitsLog.length : 0;
            for (let i = 0; i < len; i++) {
                const log = orgLimitsLog[i];
                const label = log[0];
                const value = parseFloat(log[2]) ? Math.round(100 * (parseFloat(log[2]) - parseFloat(log[1])) / parseFloat(log[2]), 2) : 0;
                const time = log[3];
                const logColor = colors[i % (colors.length - 1)];
                if (!dataLookup[label]) dataLookup[label] = {
                    label,
                    backgroundColor: color(logColor).alpha(0.5).rgbString(),
                    borderColor: logColor,
                    fill: false,
                    data: [],
                    hidden: !!this.hidden[label]
                };
                dataLookup[label].data.push({ x: time, y: value });
            }
            const dataValues = Object.values(dataLookup);
            const datasets = [];
            const l = dataValues ? dataValues.length : 0;
            for (let i = 0; i < l; i++) {
                const dat = dataValues[i];
                datasets.push(dat);
            }
            return {
                labels: [ // Date Objects
                    this.newDate(0),
                    this.newDate(1),
                    this.newDate(2),
                    this.newDate(3),
                    this.newDate(4),
                    this.newDate(5),
                    this.newDate(6)
                ],
                datasets
            };
        }/*,
        userLicenses() {
            return this.$store.state.userLicenses;
        }*/
    },
    beforeDestroy() {
        if (this.chart) {
            this.chart.destroy();
            this.chart = null;
        }
    }
});