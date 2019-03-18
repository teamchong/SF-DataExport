﻿Vue.component('org-chart', {
    template,
    props: ['data'],
    data() { return { chart: null }; },
    mounted() {
        this.$nextTick(function () {
            this.createOrgChart();
        });
    },
    watch: {
        data() {
            this.createOrgChart();
        }
    },
    methods: {
        createOrgChart() {
            this.$nextTick(function () {
                const { $el, data } = this;
                const { container } = this.$refs;
                $el.style.height = 'auto';
                this.removeAllChilds(container);
                this.chart = new OrgChart({
                    chartContainer: '#' + container.id,
                    createNode: (node, data) => {
                        this.createOrgNode(node, data);
                    },
                    data,
                    direction: 'l2r',
                    nodeContent: 'name',
                    nodeTitle: '_',
                    pan: true,
                    parentNodeSymbol: '',
                    toggleSiblingsResp: true
                });
                const emptyNodes = container.querySelectorAll('tr:empty');
                const len = emptyNodes ? emptyNodes.length : 0;
                for (let i = 0; i < len; i++) {
                    emptyNodes[i].parentNode.classList.add('empty');
                }
                this.$nextTick(function () {
                    this.$el.style.height = this.chart.chart.clientWidth + 100 + 'px';
                });
            });
        },
        createOrgNode(node, { name, users, url }) {
            const rLen = (users || []).length || 0;

            const titleDiv = node.querySelector('.title');
            if (titleDiv) titleDiv.innerHTML = rLen ? ['<i class="material-icons">group</i> (', rLen, ')'].join('') : '';

            const contentDiv = node.querySelector('.content');

            if (contentDiv) {
                const container = document.createDocumentFragment();
                const orgLink = document.createElement('a');
                orgLink.href = 'javascript:void(0)';
                orgLink.addEventListener('click', _ => this.dispatch('viewPage', url), true);
                orgLink.innerText = name;
                orgLink.title = name;
                container.appendChild(orgLink);

                if (rLen) {
                    const userPanel = document.createElement('div');
                    userPanel.className = 'users';

                    for (let r = 0; r < rLen; r++) {
                        const { Id, Email, Name, Profile, SmallPhotoUrl, UserRole } = users[r];
                        const a = document.createElement('a');
                        a.href = 'javascript:void(0)';

                        if (Id) {
                            a.addEventListener('click', _ =>
                                //this.dispatch('viewPage', url.replace(/^(https:\/\/[^/]+\/).*$/i, '$1') + Id + '?noredirect=1'), true);
                                this.dispatch('popoverUserId', Id), true);
                        }

                        a.innerText = Name;
                        a.className = 'slds-truncate';
                        a.title = Name + ' ' + Email + '\n' + (UserRole || {}).Name + '\n' + (Profile || {}).Name;
                        a.style.backgroundImage = 'url(' + SmallPhotoUrl + ')';
                        userPanel.appendChild(a);
                    }

                    container.appendChild(userPanel);
                }

                this.removeAllChilds(contentDiv);
                contentDiv.appendChild(container);
            }
        },
        removeAllChilds(node) {
            if (node) {
                let lastContent = node.lastChild;
                while (lastContent) {
                    node.removeChild(lastContent);
                    lastContent = node.lastChild;
                }
            }
        }
    }
});