/**
 * Workspace Tree Navigation Module
 * Handles loading, rendering, and interaction of the workspace/project tree in the sidebar.
 * Supports "scoped" mode where only a subtree is displayed with breadcrumb navigation.
 */
const WorkspaceTree = (function() {
    const STORAGE_KEY = 'memorizer-workspace-tree-state';
    const SCOPE_STORAGE_KEY = 'memorizer-workspace-tree-scope';
    let treeContainer = null;
    let isInitialized = false;
    let treeData = null;
    let currentScope = null; // { type: 'workspace'|'project', id: guid, ancestors: [...] }
    let userClearedScope = false; // Tracks when user explicitly cleared scope (prevents auto-re-scope)

    /**
     * Get expanded state from localStorage
     */
    function getExpandedState() {
        try {
            const stored = localStorage.getItem(STORAGE_KEY);
            return stored ? JSON.parse(stored) : {};
        } catch (e) {
            console.warn('Failed to read workspace tree state:', e);
            return {};
        }
    }

    /**
     * Save expanded state to localStorage
     */
    function saveExpandedState(state) {
        try {
            localStorage.setItem(STORAGE_KEY, JSON.stringify(state));
        } catch (e) {
            console.warn('Failed to save workspace tree state:', e);
        }
    }

    /**
     * Find a workspace node by ID in the tree data
     * Returns { node, ancestors: [{type, id, name},...] } or null
     */
    function findWorkspaceNode(workspaceId, workspaces, ancestors = []) {
        if (!workspaces) return null;
        for (const ws of workspaces) {
            if (ws.id === workspaceId) {
                return { node: ws, type: 'workspace', ancestors };
            }
            // Check children
            if (ws.children && ws.children.length > 0) {
                const newAncestors = [...ancestors, { type: 'workspace', id: ws.id, name: ws.name }];
                const found = findWorkspaceNode(workspaceId, ws.children, newAncestors);
                if (found) return found;
            }
        }
        return null;
    }

    /**
     * Find a project node by ID in the tree data
     * Returns { node, workspaceAncestors: [...], projectAncestors: [...] } or null
     */
    function findProjectNode(projectId, workspaces, wsAncestors = []) {
        if (!workspaces) return null;
        for (const ws of workspaces) {
            const newWsAncestors = [...wsAncestors, { type: 'workspace', id: ws.id, name: ws.name }];
            // Check projects in this workspace
            if (ws.projects) {
                const projectResult = findProjectInProjects(projectId, ws.projects, newWsAncestors, []);
                if (projectResult) return projectResult;
            }
            // Check child workspaces
            if (ws.children && ws.children.length > 0) {
                const found = findProjectNode(projectId, ws.children, newWsAncestors);
                if (found) return found;
            }
        }
        return null;
    }

    /**
     * Search for a project within a list of projects (recursive)
     */
    function findProjectInProjects(projectId, projects, wsAncestors, projAncestors) {
        if (!projects) return null;
        for (const proj of projects) {
            if (proj.id === projectId) {
                return {
                    node: proj,
                    type: 'project',
                    ancestors: [...wsAncestors, ...projAncestors]
                };
            }
            // Check child projects
            if (proj.children && proj.children.length > 0) {
                const newProjAncestors = [...projAncestors, { type: 'project', id: proj.id, name: proj.name }];
                const found = findProjectInProjects(projectId, proj.children, wsAncestors, newProjAncestors);
                if (found) return found;
            }
        }
        return null;
    }

    /**
     * Determine scope based on current URL
     */
    function determineScopeFromUrl() {
        const path = window.location.pathname;
        const workspaceMatch = path.match(/\/ui\/workspaces\/([0-9a-f-]+)/i);
        const projectMatch = path.match(/\/ui\/projects\/([0-9a-f-]+)/i);

        if (projectMatch && treeData) {
            const projectId = projectMatch[1];
            const found = findProjectNode(projectId, treeData.workspaces);
            if (found) {
                return {
                    type: 'project',
                    id: projectId,
                    node: found.node,
                    ancestors: found.ancestors
                };
            }
        } else if (workspaceMatch && treeData) {
            const workspaceId = workspaceMatch[1];
            const found = findWorkspaceNode(workspaceId, treeData.workspaces);
            if (found) {
                return {
                    type: 'workspace',
                    id: workspaceId,
                    node: found.node,
                    ancestors: found.ancestors
                };
            }
        }
        return null;
    }

    /**
     * Set scope and re-render
     */
    function setScope(type, id) {
        if (!treeData) return;

        // User is explicitly setting scope, so clear the "user cleared" flag
        userClearedScope = false;

        if (type === null || id === null) {
            currentScope = null;
        } else if (type === 'workspace') {
            const found = findWorkspaceNode(id, treeData.workspaces);
            if (found) {
                currentScope = { type, id, node: found.node, ancestors: found.ancestors };
            }
        } else if (type === 'project') {
            const found = findProjectNode(id, treeData.workspaces);
            if (found) {
                currentScope = { type, id, node: found.node, ancestors: found.ancestors };
            }
        }
        render(treeData);
    }

    /**
     * Clear scope and show all workspaces
     */
    function clearScope() {
        currentScope = null;
        userClearedScope = true; // Prevent auto-re-scope until URL actually changes
        render(treeData);
    }

    /**
     * Render breadcrumb navigation for scoped view
     */
    function renderBreadcrumbs(scope) {
        if (!scope || !scope.ancestors || scope.ancestors.length === 0) {
            // Just show "All Workspaces" button
            return `
                <div class="workspace-tree-scope-header">
                    <a href="#" class="workspace-tree-scope-back"
                       onclick="event.preventDefault(); WorkspaceTree.clearScope();"
                       title="Return to all workspaces">
                        <i class="fas fa-arrow-left"></i> All Workspaces
                    </a>
                </div>`;
        }

        let html = `<div class="workspace-tree-scope-header">`;
        html += `<a href="#" class="workspace-tree-scope-back"
                    onclick="event.preventDefault(); WorkspaceTree.clearScope();"
                    title="Return to all workspaces">
                    <i class="fas fa-arrow-left"></i> All
                 </a>`;
        html += `</div>`;

        // Breadcrumb trail
        html += `<div class="workspace-tree-breadcrumbs">`;
        for (let i = 0; i < scope.ancestors.length; i++) {
            const ancestor = scope.ancestors[i];
            if (i > 0) {
                html += `<span class="workspace-tree-breadcrumb-sep"><i class="fas fa-chevron-right"></i></span>`;
            }
            html += `<a href="#" class="workspace-tree-breadcrumb-item"
                        onclick="event.preventDefault(); WorkspaceTree.setScope('${ancestor.type}', '${ancestor.id}');"
                        title="${escapeHtml(ancestor.name)}">
                        ${escapeHtml(truncateName(ancestor.name, 15))}
                     </a>`;
        }
        html += `</div>`;
        return html;
    }

    /**
     * Truncate name for breadcrumb display
     */
    function truncateName(name, maxLength) {
        if (!name || name.length <= maxLength) return name;
        return name.substring(0, maxLength - 1) + '…';
    }

    /**
     * Toggle expand/collapse for a node
     */
    function toggleNode(nodeId, nodeType) {
        const state = getExpandedState();
        const key = `${nodeType}-${nodeId}`;
        state[key] = !state[key];
        saveExpandedState(state);

        // Update DOM
        const item = document.querySelector(`[data-node-id="${nodeId}"][data-node-type="${nodeType}"]`);
        if (item) {
            const toggle = item.querySelector('.workspace-tree-toggle');
            const children = item.querySelector('.workspace-tree-children');

            if (state[key]) {
                toggle?.classList.add('expanded');
                children?.classList.add('expanded');
            } else {
                toggle?.classList.remove('expanded');
                children?.classList.remove('expanded');
            }
        }
    }

    /**
     * Check if a node is expanded
     * @param {string} nodeId - The node ID
     * @param {string} nodeType - 'workspace' or 'project'
     * @param {number} level - Nesting level (0 = root)
     */
    function isExpanded(nodeId, nodeType, level = 0) {
        const state = getExpandedState();
        const key = `${nodeType}-${nodeId}`;

        // If user has explicitly set a state, use it
        if (state[key] !== undefined) {
            return state[key];
        }

        // Default: expand root workspaces (level 0), collapse nested ones
        if (nodeType === 'workspace') {
            return level === 0;
        }

        // Projects: expand at levels 0-1, collapse at deeper levels
        return level <= 1;
    }

    /**
     * Render a workspace node
     */
    function renderWorkspaceNode(workspace, level) {
        const hasChildren = (workspace.projects && workspace.projects.length > 0) ||
                           (workspace.children && workspace.children.length > 0);
        const expanded = isExpanded(workspace.id, 'workspace', level);

        let html = `
            <li class="workspace-tree-item workspace-tree-workspace"
                data-node-id="${workspace.id}"
                data-node-type="workspace"
                data-level="${level}">
                <a class="workspace-tree-link"
                   href="/workspaces/${workspace.id}"
                   title="${escapeHtml(workspace.description || workspace.name)}">
                    <span class="workspace-tree-toggle ${hasChildren ? (expanded ? 'expanded' : '') : 'empty'}"
                          onclick="event.preventDefault(); event.stopPropagation(); WorkspaceTree.toggle('${workspace.id}', 'workspace');">
                        <i class="fas fa-chevron-right"></i>
                    </span>
                    <span class="workspace-tree-icon">
                        <i class="fas fa-folder${expanded && hasChildren ? '-open' : ''}"></i>
                    </span>
                    <span class="workspace-tree-name">${escapeHtml(workspace.name)}</span>
                    <span class="workspace-tree-count">${workspace.memoryCount}</span>
                </a>`;

        if (hasChildren) {
            html += `<ul class="workspace-tree-children ${expanded ? 'expanded' : ''}">`;

            // Render projects first
            if (workspace.projects) {
                for (const project of workspace.projects) {
                    html += renderProjectNode(project, level + 1);
                }
            }

            // Then nested workspaces
            if (workspace.children) {
                for (const child of workspace.children) {
                    html += renderWorkspaceNode(child, level + 1);
                }
            }

            html += `</ul>`;
        }

        html += `</li>`;
        return html;
    }

    /**
     * Render a project node
     */
    function renderProjectNode(project, level) {
        const hasChildren = project.children && project.children.length > 0;
        const expanded = isExpanded(project.id, 'project', level);

        let html = `
            <li class="workspace-tree-item workspace-tree-project"
                data-node-id="${project.id}"
                data-node-type="project"
                data-level="${level}">
                <a class="workspace-tree-link"
                   href="/projects/${project.id}"
                   title="${escapeHtml(project.description || project.name)}">
                    <span class="workspace-tree-toggle ${hasChildren ? (expanded ? 'expanded' : '') : 'empty'}"
                          onclick="event.preventDefault(); event.stopPropagation(); WorkspaceTree.toggle('${project.id}', 'project');">
                        <i class="fas fa-chevron-right"></i>
                    </span>
                    <span class="workspace-tree-status ${project.status}" title="${formatStatusTooltip(project.status)}"></span>
                    <span class="workspace-tree-icon">
                        <i class="fas fa-tasks"></i>
                    </span>
                    <span class="workspace-tree-name">${escapeHtml(project.name)}</span>
                    <span class="workspace-tree-count">${project.memoryCount}</span>
                </a>`;

        if (hasChildren) {
            html += `<ul class="workspace-tree-children ${expanded ? 'expanded' : ''}">`;
            for (const child of project.children) {
                html += renderProjectNode(child, level + 1);
            }
            html += `</ul>`;
        }

        html += `</li>`;
        return html;
    }

    /**
     * Escape HTML to prevent XSS
     */
    function escapeHtml(text) {
        if (!text) return '';
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    /**
     * Format status for display in tooltip
     */
    function formatStatusTooltip(status) {
        const statusLabels = {
            'draft': 'Status: Draft',
            'active': 'Status: Active',
            'on_hold': 'Status: On Hold',
            'onhold': 'Status: On Hold',
            'completed': 'Status: Completed',
            'cancelled': 'Status: Cancelled',
            'archived': 'Status: Archived'
        };
        return statusLabels[status.toLowerCase()] || `Status: ${status}`;
    }

    /**
     * Render the full tree (or scoped subtree)
     */
    function render(data) {
        if (!treeContainer) return;

        treeData = data;

        // Determine if we should auto-scope based on URL (only for deep items)
        // Skip auto-scope if user explicitly cleared it
        if (!currentScope && !userClearedScope) {
            const urlScope = determineScopeFromUrl();
            // Auto-scope only if the item is at depth 2+ (has ancestors)
            if (urlScope && urlScope.ancestors && urlScope.ancestors.length >= 2) {
                currentScope = urlScope;
            }
        }

        let html = '';

        if (currentScope) {
            // Scoped mode - show breadcrumbs and just the scoped subtree
            html += renderBreadcrumbs(currentScope);
            html += `<div class="workspace-tree-header">${escapeHtml(currentScope.node.name)}</div>`;

            if (currentScope.type === 'workspace') {
                // Render the scoped workspace's contents (projects and child workspaces)
                const ws = currentScope.node;
                const hasContent = (ws.projects && ws.projects.length > 0) ||
                                   (ws.children && ws.children.length > 0);
                if (!hasContent) {
                    html += `<div class="workspace-tree-empty">No projects or workspaces</div>`;
                } else {
                    html += `<ul class="workspace-tree">`;
                    // Render projects
                    if (ws.projects) {
                        for (const project of ws.projects) {
                            html += renderProjectNode(project, 0);
                        }
                    }
                    // Render child workspaces
                    if (ws.children) {
                        for (const child of ws.children) {
                            html += renderWorkspaceNode(child, 0);
                        }
                    }
                    html += `</ul>`;
                }
            } else if (currentScope.type === 'project') {
                // Render the scoped project's children
                const proj = currentScope.node;
                if (!proj.children || proj.children.length === 0) {
                    html += `<div class="workspace-tree-empty">No sub-projects</div>`;
                } else {
                    html += `<ul class="workspace-tree">`;
                    for (const child of proj.children) {
                        html += renderProjectNode(child, 0);
                    }
                    html += `</ul>`;
                }
            }
        } else {
            // Normal mode - show all workspaces
            html += `<div class="workspace-tree-header-row">
                <span class="workspace-tree-header">Workspaces</span>
                <button type="button" class="workspace-tree-add-btn" onclick="WorkspaceTree.openCreateModal()" title="Create new workspace">
                    <i class="fas fa-plus"></i>
                </button>
            </div>`;

            if (!data.workspaces || data.workspaces.length === 0) {
                html += `<div class="workspace-tree-empty">No workspaces yet</div>`;
            } else {
                html += `<ul class="workspace-tree">`;
                for (const workspace of data.workspaces) {
                    html += renderWorkspaceNode(workspace, 0);
                }
                html += `</ul>`;
            }
        }

        // Divider and Unfiled section (always shown)
        html += `
            <div class="workspace-tree-divider"></div>
            <a class="workspace-tree-unfiled" href="/memories?unfiled=true" title="Memories not assigned to any workspace or project">
                <span class="workspace-tree-icon">
                    <i class="fas fa-inbox"></i>
                </span>
                <span class="workspace-tree-name">Unfiled</span>
                <span class="workspace-tree-count">${data.unfiledCount || 0}</span>
            </a>`;

        treeContainer.innerHTML = html;

        // Highlight current selection based on URL
        highlightCurrentSelection();
    }

    /**
     * Highlight the current selection based on URL path
     */
    function highlightCurrentSelection() {
        const path = window.location.pathname;
        const params = new URLSearchParams(window.location.search);
        const unfiled = params.get('unfiled');

        // Extract IDs from path segments like /workspaces/{id} or /projects/{id}
        const workspaceMatch = path.match(/\/ui\/workspaces\/([0-9a-f-]+)/i);
        const projectMatch = path.match(/\/ui\/projects\/([0-9a-f-]+)/i);

        const workspaceId = workspaceMatch ? workspaceMatch[1] : null;
        const projectId = projectMatch ? projectMatch[1] : null;

        // Remove existing active states
        document.querySelectorAll('.workspace-tree-link.active, .workspace-tree-unfiled.active').forEach(el => {
            el.classList.remove('active');
        });

        if (projectId) {
            const projectLink = document.querySelector(`[data-node-id="${projectId}"][data-node-type="project"] > .workspace-tree-link`);
            if (projectLink) {
                projectLink.classList.add('active');
                // Ensure parent nodes are expanded
                expandParents(projectLink);
            }
        } else if (workspaceId) {
            const workspaceLink = document.querySelector(`[data-node-id="${workspaceId}"][data-node-type="workspace"] > .workspace-tree-link`);
            if (workspaceLink) {
                workspaceLink.classList.add('active');
                expandParents(workspaceLink);
            }
        } else if (unfiled === 'true') {
            const unfiledLink = document.querySelector('.workspace-tree-unfiled');
            if (unfiledLink) {
                unfiledLink.classList.add('active');
            }
        }
    }

    /**
     * Expand parent nodes to make the selected item visible
     */
    function expandParents(element) {
        let parent = element.closest('.workspace-tree-children');
        while (parent) {
            parent.classList.add('expanded');
            const item = parent.closest('.workspace-tree-item');
            if (item) {
                const toggle = item.querySelector(':scope > .workspace-tree-link > .workspace-tree-toggle');
                toggle?.classList.add('expanded');
            }
            parent = parent.parentElement?.closest('.workspace-tree-children');
        }
    }

    /**
     * Show loading state
     */
    function showLoading() {
        if (!treeContainer) return;
        treeContainer.innerHTML = `
            <div class="workspace-tree-header">Workspaces</div>
            <div class="workspace-tree-loading">
                <span class="spinner-border spinner-border-sm" role="status"></span>
                <span>Loading...</span>
            </div>`;
    }

    /**
     * Show error state
     */
    function showError(message) {
        if (!treeContainer) return;
        treeContainer.innerHTML = `
            <div class="workspace-tree-header">Workspaces</div>
            <div class="workspace-tree-error">
                <i class="fas fa-exclamation-triangle"></i> ${escapeHtml(message)}
            </div>`;
    }

    /**
     * Fetch tree data from API
     */
    async function fetchTreeData() {
        const response = await fetch('/api/workspace-tree');
        if (!response.ok) {
            throw new Error(`Failed to load workspaces: ${response.status}`);
        }
        return await response.json();
    }

    /**
     * Load and render the tree
     */
    async function load() {
        showLoading();
        try {
            const data = await fetchTreeData();
            render(data);
        } catch (e) {
            console.error('Failed to load workspace tree:', e);
            showError('Failed to load');
        }
    }

    /**
     * Refresh the tree data
     */
    async function refresh() {
        try {
            const data = await fetchTreeData();
            render(data);
        } catch (e) {
            console.error('Failed to refresh workspace tree:', e);
        }
    }

    /**
     * Initialize the workspace tree
     */
    function init() {
        if (isInitialized) return;

        treeContainer = document.getElementById('workspace-tree-container');
        if (!treeContainer) {
            console.warn('Workspace tree container not found');
            return;
        }

        isInitialized = true;
        load();

        // Listen for URL changes (for SPA-style navigation if needed)
        window.addEventListener('popstate', function() {
            // Reset scope on URL change and re-evaluate
            currentScope = null;
            userClearedScope = false; // Allow auto-scope on new URL
            if (treeData) {
                render(treeData);
            }
        });
    }

    /**
     * Open the create workspace modal
     */
    function openCreateModal() {
        const modal = document.getElementById('createWorkspaceFromSidebarModal');
        if (modal) {
            const bsModal = new bootstrap.Modal(modal);
            bsModal.show();
        }
    }

    /**
     * Create a new workspace from the sidebar modal
     */
    async function createWorkspaceFromSidebar() {
        const nameInput = document.getElementById('sidebarWorkspaceName');
        const descInput = document.getElementById('sidebarWorkspaceDescription');
        const btn = document.getElementById('sidebarCreateWorkspaceBtn');

        const name = nameInput?.value.trim();
        const description = descInput?.value.trim();

        if (!name) {
            alert('Name is required');
            return;
        }

        const originalText = btn.innerHTML;
        btn.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Creating...';
        btn.disabled = true;

        try {
            const response = await fetch('/api/workspace', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ name, description: description || null })
            });

            if (!response.ok) {
                const error = await response.json();
                throw new Error(error.message || 'Failed to create workspace');
            }

            const newWorkspace = await response.json();

            // Close modal
            const modal = document.getElementById('createWorkspaceFromSidebarModal');
            bootstrap.Modal.getInstance(modal)?.hide();

            // Clear form
            nameInput.value = '';
            descInput.value = '';

            // Refresh the tree
            await refresh();

            // Navigate to the new workspace
            window.location.href = `/workspaces/${newWorkspace.id}`;

        } catch (error) {
            alert('Error: ' + error.message);
        } finally {
            btn.innerHTML = originalText;
            btn.disabled = false;
        }
    }

    // Public API
    return {
        init: init,
        load: load,
        refresh: refresh,
        toggle: toggleNode,
        setScope: setScope,
        clearScope: clearScope,
        openCreateModal: openCreateModal,
        createWorkspace: createWorkspaceFromSidebar
    };
})();

// Initialize when DOM is ready
document.addEventListener('DOMContentLoaded', function() {
    WorkspaceTree.init();
});
