Vue.component('overview-tab', {
    template,
    data() {
        return { loadTime: new Date().getTime() };
    },
    methods: {
        filterOrgChart({ children, id, name, url, users }, filter) {
            const filteredChildren = [];
            const childLen = children ? children.length : 0;
            for (let i = 0; i < childLen; i++) {
                const child = children[i];
                const filteredChild = this.filterOrgChart(child, filter);
                if (filteredChild) {
                    filteredChildren.push(filteredChild);
                }
            }
            const filteredUsers = [];
            const usersLen = users ? users.length : 0;
            const filterEx = filter ? new RegExp(_.escapeRegExp(filter), 'i') : null;
            for (let i = 0; i < usersLen; i++) {
                const user = users[i];
                if (!filterEx || user.Name && filterEx.test(user.Name) || user.Email && filterEx.test(user.Email)) {
                    filteredUsers.push(user);
                }
            }
            if (filteredChildren.length || filteredUsers.length) {
                return { children: filteredChildren, id, name, url, users: filteredUsers };
            }
            return null;
        },
        reload() {
            this.loadTime = new Date().getTime();
        }
    },
    computed: {
        currentInstanceUrl() {
            return this.$store.state.currentInstanceUrl;
        },
        data() {
            const { orgChartSearch, userRoles } = this.$store.state;
            let filtered = this.filterOrgChart(userRoles, orgChartSearch);
            if (!filtered) {
                const { id, name, url } = userRoles;
                filtered = { id, name, url };
            }
            filtered.loadTime = this.loadTime;
            return filtered;
        },
        orgChartSearch: {
            get() {
                return this.$store.state.orgChartSearch;
            },
            set(value) {
                this.dispatch('orgChartSearch', value);
            }
        },
    },
});