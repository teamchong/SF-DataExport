Vue.component('overview-tab', {
    template,
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
            for (let i = 0; i < usersLen; i++) {
                const user = users[i];
                if (!filter || user.Name && user.Name.indexOf(filter) >= 0 || user.Email && user.Email.indexOf(filter) >= 0) {
                    filteredUsers.push(user);
                }
            }
            if (filteredChildren.length || filteredUsers.length) {
                return { children: filteredChildren, id, name, url, users: filteredUsers };
            }
            return null;
        }
    },
    computed: {
        currentInstanceUrl() {
            return this.$store.state.currentInstanceUrl;
        },
        data() {
            const { orgChartSearch, userRoles } = this.$store.state;
            const filtered = this.filterOrgChart(userRoles, orgChartSearch);
            if (!filtered) {
                const { id, name, url } = userRoles;
                return { id, name, url };
            }
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