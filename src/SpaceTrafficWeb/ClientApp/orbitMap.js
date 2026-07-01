import * as THREE from "three";
import { OrbitControls } from "three/examples/jsm/controls/OrbitControls.js";
import { CSS2DObject, CSS2DRenderer } from "three/examples/jsm/renderers/CSS2DRenderer.js";

const EARTH_RADIUS_KM = 6371;
const MAX_ALTITUDE_KM = 42000;
const ORBIT_COLORS = {
    LEO: new THREE.Color("#33d6a6"),
    MEO: new THREE.Color("#f2c94c"),
    GEO: new THREE.Color("#56a8ff"),
    HEO: new THREE.Color("#b78cff"),
    UNKNOWN: new THREE.Color("#cbd5e1")
};
const DEBRIS_COLOR = new THREE.Color("#ff6b6b");

export function initOrbitMap(container, objects) {
    return new OrbitMap(container, objects ?? []);
}

class OrbitMap {
    constructor(container, objects) {
        this.container = container;
        this.tooltip = container.querySelector("[data-orbit-tooltip]");
        this.objects = objects;
        this.pointer = new THREE.Vector2(-10, -10);
        this.raycaster = new THREE.Raycaster();
        this.dummy = new THREE.Object3D();
        this.disposed = false;

        this.scene = new THREE.Scene();
        this.scene.background = new THREE.Color("#05070d");
        this.camera = new THREE.PerspectiveCamera(45, 1, 0.1, 100);
        this.renderer = new THREE.WebGLRenderer({ antialias: true, alpha: false, powerPreference: "high-performance" });
        this.renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
        this.renderer.outputColorSpace = THREE.SRGBColorSpace;
        container.appendChild(this.renderer.domElement);
        this.labelRenderer = new CSS2DRenderer();
        this.labelRenderer.domElement.className = "orbit-label-layer";
        container.appendChild(this.labelRenderer.domElement);

        this.root = new THREE.Group();
        this.scene.add(this.root);
        this.root.rotation.set(-0.25, 0.45, 0);
        this.tooltipObject = new CSS2DObject(this.tooltip);
        this.tooltipObject.visible = false;
        this.scene.add(this.tooltipObject);

        this.addLights();
        this.addStars();
        this.addEarth();
        this.addOrbitBands();
        this.addObjects();
        this.addControls();
        this.bindEvents();
        this.resize();
        this.animate();
    }

    addLights() {
        this.scene.add(new THREE.AmbientLight("#7b8cff", 0.45));
        const sun = new THREE.DirectionalLight("#ffffff", 2.6);
        sun.position.set(4, 3, 5);
        this.scene.add(sun);
        const rim = new THREE.DirectionalLight("#62d6ff", 0.9);
        rim.position.set(-5, -2, -3);
        this.scene.add(rim);
    }

    addStars() {
        const count = 900;
        const positions = new Float32Array(count * 3);
        for (let i = 0; i < count; i += 1) {
            const radius = 18 + hash01(i) * 22;
            const theta = hash01(i * 17 + 3) * Math.PI * 2;
            const phi = Math.acos(hash01(i * 31 + 7) * 2 - 1);
            positions[i * 3] = radius * Math.sin(phi) * Math.cos(theta);
            positions[i * 3 + 1] = radius * Math.cos(phi);
            positions[i * 3 + 2] = radius * Math.sin(phi) * Math.sin(theta);
        }
        const geometry = new THREE.BufferGeometry();
        geometry.setAttribute("position", new THREE.BufferAttribute(positions, 3));
        const material = new THREE.PointsMaterial({ color: "#dbeafe", size: 0.018, transparent: true, opacity: 0.7 });
        this.scene.add(new THREE.Points(geometry, material));
    }

    addEarth() {
        const earthGeometry = new THREE.SphereGeometry(1, 96, 64);
        const earthMaterial = new THREE.MeshStandardMaterial({
            map: createEarthTexture(),
            roughness: 0.82,
            metalness: 0.02
        });
        this.earth = new THREE.Mesh(earthGeometry, earthMaterial);
        this.root.add(this.earth);

        const atmosphereGeometry = new THREE.SphereGeometry(1.045, 96, 64);
        const atmosphereMaterial = new THREE.MeshBasicMaterial({
            color: "#59c7ff",
            transparent: true,
            opacity: 0.16,
            side: THREE.BackSide
        });
        this.root.add(new THREE.Mesh(atmosphereGeometry, atmosphereMaterial));
    }

    addOrbitBands() {
        const bands = [
            { orbitClass: "LEO", altitude: 550, inclination: 52 },
            { orbitClass: "MEO", altitude: 20200, inclination: 56 },
            { orbitClass: "GEO", altitude: 35786, inclination: 0 },
            { orbitClass: "HEO", altitude: 42000, inclination: 63 }
        ];

        for (const band of bands) {
            const ring = createOrbitRing(sceneRadius(band.altitude), band.inclination, ORBIT_COLORS[band.orbitClass], 0.65);
            this.root.add(ring);
        }

        const sampleCount = Math.min(this.objects.length, 360);
        const step = Math.max(1, Math.floor(this.objects.length / Math.max(sampleCount, 1)));
        for (let i = 0; i < this.objects.length; i += step) {
            const item = this.objects[i];
            const color = item.objectType?.toUpperCase().includes("DEBRIS")
                ? DEBRIS_COLOR
                : colorForOrbit(item.orbitClass);
            const ring = createOrbitRing(
                sceneRadius(item.altitudeKm),
                item.inclinationDeg ?? defaultInclination(item.orbitClass),
                color,
                0.08
            );
            ring.rotation.y = hash01(item.objectId) * Math.PI * 2;
            this.root.add(ring);
        }
    }

    addObjects() {
        const geometry = new THREE.SphereGeometry(0.018, 8, 8);
        const material = new THREE.MeshStandardMaterial({
            roughness: 0.35,
            metalness: 0.05
        });
        this.objectMesh = new THREE.InstancedMesh(geometry, material, this.objects.length);
        this.objectMesh.instanceMatrix.setUsage(THREE.DynamicDrawUsage);
        this.positions = new Array(this.objects.length);

        for (let i = 0; i < this.objects.length; i += 1) {
            const item = this.objects[i];
            const radius = sceneRadius(item.altitudeKm);
            const inclination = THREE.MathUtils.degToRad(item.inclinationDeg ?? defaultInclination(item.orbitClass));
            const phase = hash01(item.objectId) * Math.PI * 2;
            const node = hash01(item.objectId * 13 + 5) * Math.PI * 2;
            const position = orbitalPosition(radius, inclination, phase, node);

            this.dummy.position.copy(position);
            const scale = item.objectType?.toUpperCase().includes("DEBRIS") ? 0.72 : 1;
            this.dummy.scale.setScalar(scale);
            this.dummy.updateMatrix();
            this.objectMesh.setMatrixAt(i, this.dummy.matrix);
            this.objectMesh.setColorAt(i, item.objectType?.toUpperCase().includes("DEBRIS") ? DEBRIS_COLOR : colorForOrbit(item.orbitClass));
            this.positions[i] = position;
        }

        this.root.add(this.objectMesh);
    }

    addControls() {
        this.controls = new OrbitControls(this.camera, this.renderer.domElement);
        this.controls.enableDamping = true;
        this.controls.dampingFactor = 0.08;
        this.controls.enablePan = false;
        this.controls.minDistance = 3.2;
        this.controls.maxDistance = 9.5;
        this.controls.target.set(0, 0, 0);
        this.camera.position.set(0, 0.4, 5.2);
        this.controls.update();
    }

    bindEvents() {
        this.onResize = () => this.resize();
        this.onPointerMove = (event) => this.pointerMove(event);

        window.addEventListener("resize", this.onResize);
        this.renderer.domElement.addEventListener("pointermove", this.onPointerMove);
    }

    pointerMove(event) {
        const rect = this.renderer.domElement.getBoundingClientRect();
        this.pointer.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
        this.pointer.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;

    }

    resize() {
        const rect = this.container.getBoundingClientRect();
        const width = Math.max(320, rect.width);
        const height = Math.max(420, rect.height);
        this.camera.aspect = width / height;
        this.camera.updateProjectionMatrix();
        this.renderer.setSize(width, height, false);
        this.labelRenderer.setSize(width, height);
    }

    animate() {
        if (this.disposed) {
            return;
        }

        this.animationFrame = requestAnimationFrame(() => this.animate());

        this.earth.rotation.y += 0.0015;
        this.controls.update();

        this.updateHover();
        this.renderer.render(this.scene, this.camera);
        this.labelRenderer.render(this.scene, this.camera);
    }

    updateHover() {
        if (!this.objectMesh || this.objects.length === 0) {
            this.hideTooltip();
            return;
        }

        this.raycaster.setFromCamera(this.pointer, this.camera);
        const hit = this.raycaster.intersectObject(this.objectMesh, false)[0];
        if (!hit || hit.instanceId === undefined) {
            this.hideTooltip();
            return;
        }

        const item = this.objects[hit.instanceId];
        this.tooltipObject.position.copy(hit.point);
        this.tooltipObject.visible = true;
        this.tooltip.classList.add("visible");
        this.tooltip.innerHTML = tooltipHtml(item);
    }

    hideTooltip() {
        if (this.tooltipObject) {
            this.tooltipObject.visible = false;
        }
        this.tooltip?.classList.remove("visible");
    }

    dispose() {
        this.disposed = true;
        cancelAnimationFrame(this.animationFrame);
        window.removeEventListener("resize", this.onResize);
        this.renderer.domElement.removeEventListener("pointermove", this.onPointerMove);
        this.controls.dispose();
        this.scene.traverse((item) => {
            item.geometry?.dispose?.();
            if (Array.isArray(item.material)) {
                item.material.forEach((material) => material.dispose?.());
            } else {
                item.material?.dispose?.();
            }
        });
        this.renderer.dispose();
        this.labelRenderer.domElement.remove();
        this.renderer.domElement.remove();
    }
}

function createEarthTexture() {
    const canvas = document.createElement("canvas");
    canvas.width = 1024;
    canvas.height = 512;
    const context = canvas.getContext("2d");
    const gradient = context.createLinearGradient(0, 0, 0, canvas.height);
    gradient.addColorStop(0, "#123b6d");
    gradient.addColorStop(0.5, "#0e5d8c");
    gradient.addColorStop(1, "#082d5a");
    context.fillStyle = gradient;
    context.fillRect(0, 0, canvas.width, canvas.height);

    context.fillStyle = "#2f8f65";
    drawLand(context, 145, 190, 118, 56, 0.2);
    drawLand(context, 285, 285, 82, 122, -0.35);
    drawLand(context, 505, 178, 145, 70, -0.08);
    drawLand(context, 650, 275, 120, 92, 0.3);
    drawLand(context, 810, 218, 168, 82, -0.2);
    drawLand(context, 900, 355, 84, 45, 0.16);

    context.strokeStyle = "rgba(255,255,255,0.22)";
    context.lineWidth = 2;
    for (let i = 0; i < 22; i += 1) {
        const y = 55 + hash01(i * 19) * 390;
        context.beginPath();
        context.moveTo(hash01(i) * 160, y);
        context.bezierCurveTo(260, y - 30, 470, y + 38, 760 + hash01(i * 3) * 190, y - 10);
        context.stroke();
    }

    return new THREE.CanvasTexture(canvas);
}

function drawLand(context, x, y, width, height, rotation) {
    context.save();
    context.translate(x, y);
    context.rotate(rotation);
    context.beginPath();
    for (let i = 0; i < 18; i += 1) {
        const angle = (i / 18) * Math.PI * 2;
        const radiusX = width * (0.55 + hash01(i + x) * 0.28);
        const radiusY = height * (0.55 + hash01(i + y) * 0.28);
        const px = Math.cos(angle) * radiusX;
        const py = Math.sin(angle) * radiusY;
        if (i === 0) {
            context.moveTo(px, py);
        } else {
            context.lineTo(px, py);
        }
    }
    context.closePath();
    context.fill();
    context.restore();
}

function createOrbitRing(radius, inclinationDeg, color, opacity) {
    const segments = 192;
    const points = [];
    const inclination = THREE.MathUtils.degToRad(inclinationDeg);
    for (let i = 0; i <= segments; i += 1) {
        const angle = (i / segments) * Math.PI * 2;
        points.push(orbitalPosition(radius, inclination, angle, 0));
    }
    const geometry = new THREE.BufferGeometry().setFromPoints(points);
    const material = new THREE.LineBasicMaterial({ color, transparent: true, opacity, depthWrite: false });
    return new THREE.Line(geometry, material);
}

function orbitalPosition(radius, inclination, phase, node) {
    const x = Math.cos(phase) * radius;
    const z = Math.sin(phase) * radius;
    const y = Math.sin(phase) * Math.sin(inclination) * radius * 0.75;
    const flattenedZ = z * Math.cos(inclination * 0.45);
    const cosNode = Math.cos(node);
    const sinNode = Math.sin(node);
    return new THREE.Vector3(
        x * cosNode - flattenedZ * sinNode,
        y,
        x * sinNode + flattenedZ * cosNode
    );
}

function sceneRadius(altitudeKm) {
    const altitude = Math.max(160, Math.min(altitudeKm ?? 550, MAX_ALTITUDE_KM));
    const normalized = Math.log1p(altitude) / Math.log1p(MAX_ALTITUDE_KM);
    return 1.18 + normalized * 2.28;
}

function colorForOrbit(orbitClass) {
    const key = (orbitClass || "UNKNOWN").toUpperCase();
    return ORBIT_COLORS[key] ?? ORBIT_COLORS.UNKNOWN;
}

function defaultInclination(orbitClass) {
    const key = (orbitClass || "").toUpperCase();
    if (key === "GEO") return 0;
    if (key === "MEO") return 56;
    if (key === "HEO") return 63;
    return 52;
}

function hash01(value) {
    const x = Math.sin(Number(value) * 12.9898) * 43758.5453;
    return x - Math.floor(x);
}

function tooltipHtml(item) {
    return `
        <strong>${escapeHtml(item.objectName || "UNKNOWN")}</strong>
        <dl>
            <dt>NORAD</dt><dd>${item.noradId ?? "n/a"}</dd>
            <dt>Type</dt><dd>${escapeHtml(item.objectType || "n/a")}</dd>
            <dt>Orbit</dt><dd>${escapeHtml(item.orbitClass || "n/a")}</dd>
            <dt>Altitude</dt><dd>${formatNumber(item.altitudeKm)} km</dd>
            <dt>Speed</dt><dd>${formatNumber(item.velocityKmS)} km/s</dd>
            <dt>Inclination</dt><dd>${formatNumber(item.inclinationDeg)} deg</dd>
            <dt>Risk</dt><dd>${formatNumber(item.debrisRiskScore)}</dd>
            <dt>Operator</dt><dd>${escapeHtml(item.operatorName || "n/a")}</dd>
            <dt>Source</dt><dd>${escapeHtml(item.sourceName || "n/a")}</dd>
        </dl>`;
}

function formatNumber(value) {
    if (value === null || value === undefined || Number.isNaN(Number(value))) {
        return "n/a";
    }
    return Number(value).toLocaleString(undefined, { maximumFractionDigits: 2 });
}

function escapeHtml(value) {
    return String(value)
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll('"', "&quot;")
        .replaceAll("'", "&#039;");
}
