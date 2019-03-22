Vue.component('org-chart', {
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
                $el.style.visibility = 'hidden';
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
            
                this.$nextTick(function () {
                    const emptyNodes = container.querySelectorAll('tr:empty');
                    const len = emptyNodes ? emptyNodes.length : 0;
                    for (let i = 0; i < len; i++) {
                        emptyNodes[i].parentNode.classList.add('empty');
                    }

                    const contentDivs = container.querySelectorAll('.content');
                    const contentLen = contentDivs ? contentDivs.length : 0;
                    for (let i = 0; i < contentLen; i++) {
                        const contentDiv = contentDivs[i];
                        const { scrollWidth } = contentDivs[i];
                        const titleDiv = contentDiv.previousElementSibling;
                        const width = Math.max(160, scrollWidth + 10) + 'px';
                        titleDiv.style.width = width;
                        contentDiv.style.width = width;
                        contentDiv.parentNode.style.height = width;
                    }
                    
                    //$el.style.height = this.chart.chart.scrollWidth + 100 + 'px';
                    this.$el.style.visibility = 'visible';
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
                orgLink.addEventListener('click', _ => this.dispatch('ViewPage', url), true);
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
                                //this.dispatch('ViewPage', url.replace(/^(https:\/\/[^/]+\/).*$/i, '$1') + Id + '?noredirect=1'), true);
                                this.dispatch('popoverUserId', Id), true);
                        }

                        a.innerText = Name;
                        a.className = 'node-content';
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